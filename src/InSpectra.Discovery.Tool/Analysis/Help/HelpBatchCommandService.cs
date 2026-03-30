namespace InSpectra.Discovery.Tool.Analysis.Help;

using System.Text.Json.Nodes;

internal sealed class HelpBatchCommandService
{
    private readonly IHelpBatchRunner _helpRunner;
    private readonly ICliFxBatchRunner _cliFxRunner;
    private readonly IStaticBatchRunner _staticRunner;

    public HelpBatchCommandService()
        : this(new ToolHelpBatchRunner(), new CliFxBatchRunner(), new StaticBatchRunner())
    {
    }

    internal HelpBatchCommandService(IHelpBatchRunner helpRunner, ICliFxBatchRunner cliFxRunner, IStaticBatchRunner staticRunner)
    {
        _helpRunner = helpRunner;
        _cliFxRunner = cliFxRunner;
        _staticRunner = staticRunner;
    }

    public async Task<int> RunAsync(
        string repositoryRoot,
        string planPath,
        string outputRoot,
        string? batchId,
        string source,
        string targetBranch,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var planFile = Path.GetFullPath(Path.Combine(root, planPath));
        var downloadRoot = Path.GetFullPath(Path.Combine(root, outputRoot));
        var plan = HelpBatchPlan.Load(planFile);
        if (plan.Items.Count == 0)
        {
            throw new InvalidOperationException($"Plan '{planFile}' does not contain any items.");
        }

        var resolvedBatchId = string.IsNullOrWhiteSpace(batchId) ? plan.BatchId : batchId;
        if (string.IsNullOrWhiteSpace(resolvedBatchId))
        {
            throw new InvalidOperationException("A batch id is required either via `--batch-id` or the plan file.");
        }

        var timeouts = new HelpBatchTimeouts(
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds);
        var snapshotLookup = HelpBatchResultSupport.LoadCurrentSnapshotLookup(root);
        var expectedItems = new JsonArray();
        var skippedItems = new JsonArray();
        var failures = new List<string>();
        var selectedCount = 0;

        Directory.CreateDirectory(downloadRoot);
        Directory.CreateDirectory(Path.Combine(downloadRoot, "plan"));

        foreach (var item in plan.Items)
        {
            if (!string.Equals(item.AnalysisMode, "help", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.AnalysisMode, "clifx", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.AnalysisMode, "static", StringComparison.OrdinalIgnoreCase))
            {
                skippedItems.Add(HelpBatchResultSupport.CreateSkippedItem(
                    item,
                    $"analysisMode '{item.AnalysisMode}' is not supported by run-help-batch."));
                continue;
            }

            var outcome = await RunItemAsync(
                downloadRoot,
                resolvedBatchId,
                source,
                item,
                timeouts,
                snapshotLookup,
                cancellationToken);
            selectedCount++;
            expectedItems.Add(outcome.ExpectedItem);

            if (outcome.Success)
            {
                continue;
            }

            failures.Add(outcome.FailureSummary);
        }

        var expectedPath = Path.Combine(downloadRoot, "plan", "expected.json");
        RepositoryPathResolver.WriteJsonFile(expectedPath, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["batchId"] = resolvedBatchId,
            ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["sourcePlanPath"] = RepositoryPathResolver.GetRelativePath(root, planFile),
            ["sourceSnapshotPath"] = "state/discovery/dotnet-tools.current.json",
            ["targetBranch"] = targetBranch,
            ["selectedCount"] = selectedCount,
            ["skippedCount"] = skippedItems.Count,
            ["items"] = expectedItems,
            ["skipped"] = skippedItems,
        });

        var output = Runtime.CreateOutput();
        if (failures.Count > 0)
        {
            return await output.WriteErrorAsync(
                kind: "partial-failure",
                message: $"Help batch completed with {failures.Count} failure(s) out of {selectedCount} runnable item(s). Expected plan: {expectedPath}. First failure: {failures[0]}",
                exitCode: 1,
                json: json,
                cancellationToken: cancellationToken);
        }

        return await output.WriteSuccessAsync(
            new
            {
                batchId = resolvedBatchId,
                selectedCount,
                skippedCount = skippedItems.Count,
                successCount = selectedCount,
                expectedPlanPath = expectedPath,
            },
            [
                new SummaryRow("Batch", resolvedBatchId),
                new SummaryRow("Selected items", selectedCount.ToString()),
                new SummaryRow("Skipped items", skippedItems.Count.ToString()),
                new SummaryRow("Expected plan", expectedPath),
            ],
            json,
            cancellationToken);
    }

    private async Task<HelpBatchItemOutcome> RunItemAsync(
        string downloadRoot,
        string batchId,
        string source,
        HelpBatchItem item,
        HelpBatchTimeouts timeouts,
        IReadOnlyDictionary<string, HelpBatchSnapshotItem> snapshotLookup,
        CancellationToken cancellationToken)
    {
        var artifactName = HelpBatchArtifactSupport.ResolveArtifactName(item);
        var itemOutputRoot = Path.Combine(downloadRoot, artifactName);
        var exitCode = string.Equals(item.AnalysisMode, "clifx", StringComparison.OrdinalIgnoreCase)
            ? await _cliFxRunner.RunAsync(item, itemOutputRoot, batchId, source, timeouts, cancellationToken)
            : string.Equals(item.AnalysisMode, "static", StringComparison.OrdinalIgnoreCase)
                ? await _staticRunner.RunAsync(item, itemOutputRoot, batchId, source, timeouts, cancellationToken)
                : await _helpRunner.RunAsync(item, itemOutputRoot, batchId, source, timeouts, cancellationToken);
        return HelpBatchResultSupport.CreateOutcome(item, artifactName, itemOutputRoot, exitCode, snapshotLookup);
    }
}


