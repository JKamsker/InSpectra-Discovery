using System.IO.Compression;
using System.Text.Json.Nodes;
using InSpectra.Discovery.Tool.Analysis;

internal sealed class QueueCommandService
{
    private readonly IToolDescriptorResolver _descriptorResolver;

    public QueueCommandService()
        : this(new ToolDescriptorResolver())
    {
    }

    internal QueueCommandService(IToolDescriptorResolver descriptorResolver)
    {
        _descriptorResolver = descriptorResolver;
    }

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
        var prefix = QueueCommandSupport.GetSanitizedBatchPrefix(batchPrefix, targetBranch, timestampSeed);

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

        var output = Runtime.CreateOutput();
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

        using var scope = Runtime.CreateNuGetApiClientScope();
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
                if (status is "success" or "terminal-failure")
                {
                    skippedItems.Add(new { packageId, version, reason = $"existing-{status}" });
                    continue;
                }

                if (status == "terminal-negative" && !QueueCommandSupport.IsLegacyTerminalNegativeState(stateDocument))
                {
                    skippedItems.Add(new { packageId, version, reason = "existing-terminal-negative" });
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

            var runnerSelection = await RunnerSelectionResolver.ResolveForPlanItemAsync(item, skipRunnerInspection, scope.Client, cancellationToken);
            ToolDescriptor? analysisDescriptor = null;
            string? analysisDescriptorError = null;
            try
            {
                analysisDescriptor = await _descriptorResolver.ResolveAsync(packageId, version, cancellationToken);
            }
            catch (Exception ex)
            {
                analysisDescriptorError = ex.Message;
            }

            selectedItems.Add(new UntrustedBatchPlanItem(
                PackageId: packageId,
                Version: version,
                TotalDownloads: item["totalDownloads"]?.GetValue<long?>(),
                PackageUrl: item["packageUrl"]?.GetValue<string>(),
                PackageContentUrl: item["packageContentUrl"]?.GetValue<string>(),
                CatalogEntryUrl: item["catalogEntryUrl"]?.GetValue<string>(),
                Command: analysisDescriptor?.CommandName ?? item["command"]?.GetValue<string>(),
                CliFramework: analysisDescriptor?.CliFramework ?? item["cliFramework"]?.GetValue<string>(),
                AnalysisMode: analysisDescriptor?.PreferredAnalysisMode ?? item["analysisMode"]?.GetValue<string>() ?? "auto",
                AnalysisReason: analysisDescriptor?.SelectionReason ?? analysisDescriptorError,
                Attempt: (stateDocument?["attemptCount"]?.GetValue<int?>() ?? 0) + 1,
                ArtifactName: QueueCommandSupport.GetArtifactName(lowerId, lowerVersion),
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

        var output = Runtime.CreateOutput();
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

}
