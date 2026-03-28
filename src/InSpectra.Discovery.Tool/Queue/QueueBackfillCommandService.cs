using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed class QueueBackfillCommandService
{
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
                if (string.IsNullOrWhiteSpace(version))
                {
                    continue;
                }

                existingVersions.Add(version);
                indexedVersionCount++;
            }

            var runnerHint = RunnerSelectionResolver.GetHistoricalHint(root, packageId);
            var registrationIndexUrl = package["registrationUrl"]?.GetValue<string>()
                ?? $"https://api.nuget.org/v3/registration5-gz-semver2/{packageId.ToLowerInvariant()}/index.json";
            var registrationIndex = await scope.Client.GetRegistrationIndexByUrlAsync(registrationIndexUrl, cancellationToken);
            foreach (var leaf in await QueueCommandSupport.GetRegistrationLeavesAsync(scope.Client, registrationIndex, cancellationToken))
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

    public async Task<int> BuildLegacyTerminalNegativeQueueAsync(
        string repositoryRoot,
        string currentSnapshotPath,
        string outputPath,
        int? take,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var builder = new LegacyTerminalNegativeQueueBuilder();
        var computation = await builder.RunAsync(root, currentSnapshotPath, take, cancellationToken);
        RepositoryPathResolver.WriteJsonFile(outputPath, computation.Queue);

        var output = ToolRuntime.CreateOutput();
        return await output.WriteSuccessAsync(
            new
            {
                currentPackageCount = computation.CurrentPackageCount,
                eligibleLegacyNegativeCount = computation.EligibleLegacyNegativeCount,
                queueCount = computation.Queue.ItemCount,
                outputPath = Path.GetFullPath(outputPath),
            },
            [
                new SummaryRow("Current packages", computation.CurrentPackageCount.ToString()),
                new SummaryRow("Legacy negatives", computation.EligibleLegacyNegativeCount.ToString()),
                new SummaryRow("Queued current packages", computation.Queue.ItemCount.ToString()),
                new SummaryRow("Queue path", Path.GetFullPath(outputPath)),
            ],
            json,
            cancellationToken);
    }

    public async Task<int> BuildCurrentAnalysisBackfillQueueAsync(
        string repositoryRoot,
        string currentSnapshotPath,
        string outputPath,
        int? take,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var builder = new CurrentAnalysisBackfillQueueBuilder();
        var computation = await builder.RunAsync(root, currentSnapshotPath, take, cancellationToken);
        RepositoryPathResolver.WriteJsonFile(outputPath, computation.Queue);

        var output = ToolRuntime.CreateOutput();
        return await output.WriteSuccessAsync(
            new
            {
                currentPackageCount = computation.CurrentPackageCount,
                eligiblePackageCount = computation.EligiblePackageCount,
                missingCount = computation.MissingCount,
                legacyTerminalNegativeCount = computation.LegacyTerminalNegativeCount,
                legacyTerminalFailureCount = computation.LegacyTerminalFailureCount,
                retryableCount = computation.RetryableCount,
                queueCount = computation.Queue.ItemCount,
                outputPath = Path.GetFullPath(outputPath),
            },
            [
                new SummaryRow("Current packages", computation.CurrentPackageCount.ToString()),
                new SummaryRow("Eligible backlog", computation.EligiblePackageCount.ToString()),
                new SummaryRow("Missing current", computation.MissingCount.ToString()),
                new SummaryRow("Legacy negatives", computation.LegacyTerminalNegativeCount.ToString()),
                new SummaryRow("Legacy failures", computation.LegacyTerminalFailureCount.ToString()),
                new SummaryRow("Retryable current", computation.RetryableCount.ToString()),
                new SummaryRow("Queued current packages", computation.Queue.ItemCount.ToString()),
                new SummaryRow("Queue path", Path.GetFullPath(outputPath)),
            ],
            json,
            cancellationToken);
    }
}
