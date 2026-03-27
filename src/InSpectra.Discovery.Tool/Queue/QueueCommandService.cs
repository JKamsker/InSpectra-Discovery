using System.IO.Compression;
using System.Text.Json.Nodes;

internal sealed class QueueCommandService
{
    public Task<int> BuildDispatchPlanAsync(
        string queuePath,
        string targetBranch,
        string stateBranch,
        string batchPrefix,
        int batchSize,
        string outputPath,
        bool json,
        CancellationToken cancellationToken)
    {
        var repositoryRoot = RepositoryPathResolver.ResolveRepositoryRoot();
        var queueFilePath = Path.GetFullPath(queuePath);
        var queueDocument = JsonNode.Parse(File.ReadAllText(queueFilePath))?.AsObject()
            ?? throw new InvalidOperationException($"Queue file '{queueFilePath}' is empty.");
        var items = queueDocument["items"]?.AsArray() ?? [];
        var timestampSeed = queueDocument["cursorEndUtc"]?.GetValue<string>();
        var prefix = GetSanitizedBatchPrefix(batchPrefix, targetBranch, timestampSeed);

        var batches = new List<QueueDispatchBatch>();
        for (var offset = 0; offset < items.Count; offset += batchSize)
        {
            var take = Math.Min(batchSize, items.Count - offset);
            var part = batches.Count + 1;
            batches.Add(new QueueDispatchBatch(
                BatchId: $"{prefix}-{part:000}",
                QueuePath: RepositoryPathResolver.GetRelativePath(repositoryRoot, queueFilePath),
                Offset: offset,
                Take: take,
                TargetBranch: targetBranch,
                StateBranch: stateBranch,
                ItemCount: take));
        }

        var plan = new QueueDispatchPlan(
            SchemaVersion: 1,
            GeneratedAt: DateTimeOffset.UtcNow,
            QueuePath: RepositoryPathResolver.GetRelativePath(repositoryRoot, queueFilePath),
            TargetBranch: targetBranch,
            StateBranch: stateBranch,
            QueueItemCount: items.Count,
            BatchSize: batchSize,
            BatchCount: batches.Count,
            Batches: batches);

        RepositoryPathResolver.WriteJsonFile(outputPath, plan);

        var output = ToolRuntime.CreateOutput();
        return output.WriteSuccessAsync(
            plan,
            [
                new SummaryRow("Queue items", items.Count.ToString()),
                new SummaryRow("Dispatch batches", batches.Count.ToString()),
                new SummaryRow("Target branch", targetBranch),
                new SummaryRow("State branch", stateBranch),
                new SummaryRow("Output", Path.GetFullPath(outputPath)),
            ],
            json,
            cancellationToken);
    }

    public async Task<int> BuildIndexedMetadataBackfillQueueAsync(
        string repositoryRoot,
        string indexPath,
        string outputPath,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var indexFile = Path.GetFullPath(Path.Combine(root, indexPath));
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(indexFile, cancellationToken))?.AsObject()
            ?? throw new InvalidOperationException($"Index file '{indexFile}' is empty.");
        var packages = manifest["packages"]?.AsArray() ?? [];
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var items = new List<IndexedMetadataBackfillQueueItem>();
        var skipped = new List<object>();
        var indexedVersionCount = 0;

        using var scope = ToolRuntime.CreateNuGetApiClientScope();
        foreach (var packageNode in packages)
        {
            if (packageNode is not JsonObject package)
            {
                continue;
            }

            var packageId = package["packageId"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Package entry is missing packageId.");
            var existingVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var versionNode in package["versions"]?.AsArray() ?? [])
            {
                var version = versionNode?["version"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(version))
                {
                    existingVersions.Add(version);
                    indexedVersionCount++;
                }
            }

            var runnerHint = GetHistoricalRunnerHint(root, packageId);
            var registrationIndexUrl = package["registrationUrl"]?.GetValue<string>()
                ?? $"https://api.nuget.org/v3/registration5-gz-semver2/{packageId.ToLowerInvariant()}/index.json";
            var registrationIndex = await scope.Client.GetJsonByUrlAsync<RegistrationIndex>(registrationIndexUrl, cancellationToken);
            foreach (var leaf in await GetRegistrationLeavesAsync(scope.Client, registrationIndex, cancellationToken))
            {
                var version = leaf.CatalogEntry.Version;
                if (string.IsNullOrWhiteSpace(version))
                {
                    skipped.Add(new { packageId, reason = "missing-version" });
                    continue;
                }

                if (existingVersions.Contains(version))
                {
                    continue;
                }

                items.Add(new IndexedMetadataBackfillQueueItem(
                    PackageId: packageId,
                    Version: version,
                    TotalDownloads: null,
                    PackageUrl: $"https://www.nuget.org/packages/{packageId}/{version}",
                    PackageContentUrl: leaf.PackageContent,
                    RegistrationLeafUrl: leaf.Id,
                    CatalogEntryUrl: leaf.CatalogEntry.Id,
                    PublishedAt: leaf.CatalogEntry.Published?.ToUniversalTime().ToString("O"),
                    Listed: leaf.CatalogEntry.Listed,
                    BackfillKind: "indexed-package-history",
                    RunsOn: runnerHint.RunsOn,
                    RunnerReason: runnerHint.Reason,
                    RequiredFrameworks: runnerHint.RequiredFrameworks,
                    ToolRids: runnerHint.ToolRids,
                    RuntimeRids: runnerHint.RuntimeRids,
                    InspectionError: runnerHint.InspectionError,
                    RunnerHintSource: runnerHint.HintSource));
            }
        }

        var orderedItems = items
            .OrderBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.PublishedAt is null ? DateTimeOffset.MinValue : DateTimeOffset.Parse(item.PublishedAt))
            .ThenBy(item => item.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var queue = new IndexedMetadataBackfillQueue(
            SchemaVersion: 1,
            GeneratedAtUtc: generatedAtUtc,
            Filter: "indexed-package-history-backfill",
            SourceIndexPath: RepositoryPathResolver.GetRelativePath(root, indexFile),
            SourceGeneratedAtUtc: manifest["generatedAt"]?.GetValue<string>(),
            SourceCurrentSnapshotPath: RepositoryPathResolver.GetRelativePath(root, indexFile),
            IndexedPackageCount: packages.Count,
            IndexedVersionCount: indexedVersionCount,
            ItemCount: orderedItems.Count,
            BatchPrefix: "indexed-history-backfill",
            ForceReanalyze: false,
            SkipRunnerInspection: true,
            SkippedCount: skipped.Count,
            Skipped: skipped,
            Items: orderedItems);

        RepositoryPathResolver.WriteJsonFile(outputPath, queue);

        var output = ToolRuntime.CreateOutput();
        return await output.WriteSuccessAsync(
            queue,
            [
                new SummaryRow("Indexed packages", packages.Count.ToString()),
                new SummaryRow("Already indexed versions", indexedVersionCount.ToString()),
                new SummaryRow("Missing historical versions", orderedItems.Count.ToString()),
                new SummaryRow("Skipped entries", skipped.Count.ToString()),
                new SummaryRow("Queue path", Path.GetFullPath(outputPath)),
            ],
            json,
            cancellationToken);
    }

    public async Task<int> BuildUntrustedBatchPlanAsync(
        string repositoryRoot,
        string queuePath,
        string batchId,
        string outputPath,
        int offset,
        int? take,
        bool forceReanalyze,
        string targetBranch,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var queueFilePath = Path.GetFullPath(Path.Combine(root, queuePath));
        var queueDocument = JsonNode.Parse(await File.ReadAllTextAsync(queueFilePath, cancellationToken))?.AsObject()
            ?? throw new InvalidOperationException($"Queue file '{queueFilePath}' is empty.");
        var queueItems = queueDocument["items"]?.AsArray() ?? [];
        var selectedSlice = offset >= queueItems.Count
            ? []
            : queueItems.Skip(offset).Take(take ?? int.MaxValue).ToList();
        var sourceSnapshotPath = queueDocument["sourceCurrentSnapshotPath"]?.GetValue<string>()
            ?? queueDocument["inputDeltaPath"]?.GetValue<string>()
            ?? RepositoryPathResolver.GetRelativePath(root, queueFilePath);
        var skipRunnerInspection = queueDocument["skipRunnerInspection"]?.GetValue<bool?>() ?? false;

        var selectedItems = new List<UntrustedBatchPlanItem>();
        var skippedItems = new List<object>();

        using var scope = ToolRuntime.CreateNuGetApiClientScope();
        foreach (var itemNode in selectedSlice)
        {
            if (itemNode is not JsonObject item)
            {
                continue;
            }

            var packageId = item["packageId"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Queue item is missing packageId.");
            var version = item["version"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Queue item '{packageId}' is missing version.");
            var lowerId = packageId.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();
            var statePath = Path.Combine(root, "state", "packages", lowerId, $"{lowerVersion}.json");
            var stateDocument = File.Exists(statePath)
                ? JsonNode.Parse(await File.ReadAllTextAsync(statePath, cancellationToken))?.AsObject()
                : null;

            if (stateDocument is not null && !forceReanalyze)
            {
                var status = stateDocument["currentStatus"]?.GetValue<string>() ?? string.Empty;
                var nextAttemptAtText = stateDocument["nextAttemptAt"]?.GetValue<string>();
                if (status is "success" or "terminal-negative" or "terminal-failure")
                {
                    skippedItems.Add(new { packageId, version, reason = $"existing-{status}" });
                    continue;
                }

                if (status == "retryable-failure" &&
                    DateTimeOffset.TryParse(nextAttemptAtText, out var nextAttemptAt) &&
                    nextAttemptAt > DateTimeOffset.UtcNow)
                {
                    skippedItems.Add(new { packageId, version, reason = "backoff-active", nextAttemptAt = nextAttemptAt.ToString("O") });
                    continue;
                }
            }

            var runnerSelection = GetPrecomputedRunnerSelection(item, skipRunnerInspection)
                ?? await InspectPackageRunnerSelectionAsync(scope.Client, item["packageContentUrl"]?.GetValue<string>(), cancellationToken);

            selectedItems.Add(new UntrustedBatchPlanItem(
                PackageId: packageId,
                Version: version,
                TotalDownloads: item["totalDownloads"]?.GetValue<long?>(),
                PackageUrl: item["packageUrl"]?.GetValue<string>(),
                PackageContentUrl: item["packageContentUrl"]?.GetValue<string>(),
                CatalogEntryUrl: item["catalogEntryUrl"]?.GetValue<string>(),
                Attempt: (stateDocument?["attemptCount"]?.GetValue<int?>() ?? 0) + 1,
                ArtifactName: GetArtifactName(lowerId, lowerVersion),
                RunsOn: runnerSelection.RunsOn,
                RunnerReason: runnerSelection.Reason,
                RequiredFrameworks: runnerSelection.RequiredFrameworks,
                ToolRids: runnerSelection.ToolRids,
                RuntimeRids: runnerSelection.RuntimeRids,
                InspectionError: runnerSelection.InspectionError));
        }

        var plan = new UntrustedBatchPlan(
            SchemaVersion: 1,
            BatchId: batchId,
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceManifestPath: RepositoryPathResolver.GetRelativePath(root, queueFilePath),
            SourceSnapshotPath: sourceSnapshotPath,
            TargetBranch: targetBranch,
            ForceReanalyze: forceReanalyze,
            SelectedCount: selectedItems.Count,
            SkippedCount: skippedItems.Count,
            Items: selectedItems,
            Skipped: skippedItems);

        RepositoryPathResolver.WriteJsonFile(outputPath, plan);

        var output = ToolRuntime.CreateOutput();
        return await output.WriteSuccessAsync(
            plan,
            [
                new SummaryRow("Planned batch", batchId),
                new SummaryRow("Target branch", targetBranch),
                new SummaryRow("Force reanalyze", forceReanalyze.ToString()),
                new SummaryRow("Selected items", selectedItems.Count.ToString()),
                new SummaryRow("Skipped items", skippedItems.Count.ToString()),
                new SummaryRow("Output", Path.GetFullPath(outputPath)),
            ],
            json,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<RegistrationLeaf>> GetRegistrationLeavesAsync(
        NuGetApiClient client,
        RegistrationIndex index,
        CancellationToken cancellationToken)
    {
        var leaves = new List<RegistrationLeaf>();
        foreach (var page in index.Items)
        {
            if (page.Items is { Count: > 0 })
            {
                leaves.AddRange(page.Items);
                continue;
            }

            var pageDocument = await client.GetRegistrationPageAsync(page.Id, cancellationToken);
            leaves.AddRange(pageDocument.Items);
        }

        return leaves;
    }

    private static string GetSanitizedBatchPrefix(string prefix, string branchName, string? timestamp)
    {
        var normalizedPrefix = NormalizeSegment(prefix);
        var normalizedBranch = NormalizeSegment(branchName);
        var normalizedTimestamp = string.IsNullOrWhiteSpace(timestamp)
            ? DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ").ToLowerInvariant()
            : new string(timestamp.ToLowerInvariant().Where(ch => char.IsAsciiDigit(ch) || ch is 't' or 'z').ToArray());

        return $"{normalizedPrefix}-{normalizedBranch}-{normalizedTimestamp}";
    }

    private static string NormalizeSegment(string value)
    {
        var normalized = new string(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static string GetArtifactName(string lowerId, string lowerVersion)
        => new string($"analysis-{lowerId}-{lowerVersion}".Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '-').ToArray()).Trim('-');

    private static RunnerSelection GetHistoricalRunnerHint(string repositoryRoot, string packageId)
    {
        var stateDirectory = Path.Combine(repositoryRoot, "state", "packages", packageId.ToLowerInvariant());
        if (!Directory.Exists(stateDirectory))
        {
            return new RunnerSelection("ubuntu-latest", "default-ubuntu-package-history", [], [], [], null, "default");
        }

        foreach (var stateFile in Directory.GetFiles(stateDirectory, "*.json").OrderByDescending(Path.GetFileName))
        {
            JsonObject? state;
            try
            {
                state = JsonNode.Parse(File.ReadAllText(stateFile))?.AsObject();
            }
            catch
            {
                continue;
            }

            var failureText = string.Join(
                Environment.NewLine,
                new[]
                {
                    state?["lastFailureSignature"]?.GetValue<string>(),
                    state?["lastFailureMessage"]?.GetValue<string>(),
                }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (failureText.Contains("Microsoft.WindowsDesktop.App", StringComparison.OrdinalIgnoreCase))
            {
                return new RunnerSelection(
                    "windows-latest",
                    "historical-state-microsoft.windowsdesktop.app",
                    ["Microsoft.WindowsDesktop.App"],
                    [],
                    [],
                    null,
                    "historical-state");
            }
        }

        return new RunnerSelection("ubuntu-latest", "default-ubuntu-package-history", [], [], [], null, "default");
    }

    private static RunnerSelection? GetPrecomputedRunnerSelection(JsonObject item, bool skipRunnerInspection)
    {
        var runsOn = item["runsOn"]?.GetValue<string>();
        if (!skipRunnerInspection && string.IsNullOrWhiteSpace(runsOn))
        {
            return null;
        }

        return new RunnerSelection(
            RunsOn: string.IsNullOrWhiteSpace(runsOn) ? "ubuntu-latest" : runsOn,
            Reason: item["runnerReason"]?.GetValue<string>() ?? (skipRunnerInspection ? "queue-skip-runner-inspection" : "precomputed-runner-selection"),
            RequiredFrameworks: item["requiredFrameworks"]?.AsArray().Select(node => node?.GetValue<string>() ?? string.Empty).Where(value => value.Length > 0).ToList() ?? [],
            ToolRids: item["toolRids"]?.AsArray().Select(node => node?.GetValue<string>() ?? string.Empty).Where(value => value.Length > 0).ToList() ?? [],
            RuntimeRids: item["runtimeRids"]?.AsArray().Select(node => node?.GetValue<string>() ?? string.Empty).Where(value => value.Length > 0).ToList() ?? [],
            InspectionError: item["inspectionError"]?.GetValue<string>(),
            HintSource: skipRunnerInspection ? "queue" : "precomputed");
    }

    private static async Task<RunnerSelection> InspectPackageRunnerSelectionAsync(
        NuGetApiClient client,
        string? packageContentUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageContentUrl))
        {
            return new RunnerSelection("ubuntu-latest", "default-ubuntu", [], [], [], null, "inspection");
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"inspectra-batch-{Guid.NewGuid():N}.nupkg");
        try
        {
            await client.DownloadFileAsync(packageContentUrl, tempFile, cancellationToken);
            using var archive = ZipFile.OpenRead(tempFile);
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toolRids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var runtimeRids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? inspectionError = null;

            foreach (var entry in archive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/').Trim('/');
                if (string.IsNullOrWhiteSpace(entryPath))
                {
                    continue;
                }

                var segments = entryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 4 && segments[0] == "tools")
                {
                    var rid = segments[2];
                    if (!string.IsNullOrWhiteSpace(rid) && !string.Equals(rid, "any", StringComparison.OrdinalIgnoreCase))
                    {
                        toolRids.Add(rid);
                    }
                }

                if (segments.Length >= 3 && segments[0] == "runtimes")
                {
                    runtimeRids.Add(segments[1]);
                }

                if (!entryPath.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    using var reader = new StreamReader(entry.Open());
                    var runtimeConfig = JsonNode.Parse(await reader.ReadToEndAsync(cancellationToken))?.AsObject();
                    var runtimeOptions = runtimeConfig?["runtimeOptions"]?.AsObject();
                    AddFrameworkName(frameworks, runtimeOptions?["framework"]);
                    foreach (var frameworkNode in runtimeOptions?["frameworks"]?.AsArray() ?? [])
                    {
                        AddFrameworkName(frameworks, frameworkNode);
                    }
                }
                catch (Exception ex)
                {
                    inspectionError = ex.Message;
                }
            }

            var requiredFrameworks = frameworks.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
            var toolRidList = toolRids.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
            var runtimeRidList = runtimeRids.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
            var runsOn = "ubuntu-latest";
            var reason = "default-ubuntu";

            if (requiredFrameworks.Contains("Microsoft.WindowsDesktop.App", StringComparer.OrdinalIgnoreCase))
            {
                runsOn = "windows-latest";
                reason = "framework-microsoft.windowsdesktop.app";
            }
            else if (toolRidList.Count > 0 && toolRidList.All(rid => rid.StartsWith("win", StringComparison.OrdinalIgnoreCase)))
            {
                runsOn = "windows-latest";
                reason = "tool-rids-windows-only";
            }
            else if (runtimeRidList.Count > 0 && runtimeRidList.All(rid => rid.StartsWith("win", StringComparison.OrdinalIgnoreCase)))
            {
                runsOn = "windows-latest";
                reason = "runtime-rids-windows-only";
            }

            return new RunnerSelection(runsOn, reason, requiredFrameworks, toolRidList, runtimeRidList, inspectionError, "inspection");
        }
        catch (Exception ex)
        {
            return new RunnerSelection("ubuntu-latest", "default-ubuntu-inspection-failed", [], [], [], ex.Message, "inspection");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void AddFrameworkName(HashSet<string> frameworks, JsonNode? node)
    {
        if (node is null)
        {
            return;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var stringValue) && !string.IsNullOrWhiteSpace(stringValue))
        {
            frameworks.Add(stringValue.Trim());
            return;
        }

        if (node["name"]?.GetValue<string>() is { Length: > 0 } namedFramework)
        {
            frameworks.Add(namedFramework.Trim());
        }
    }
}
