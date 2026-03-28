using System.Text.Json.Nodes;

internal sealed class HelpBatchAnalysisCommandService
{
    private readonly IHelpBatchAnalysisRunner _runner;

    public HelpBatchAnalysisCommandService()
        : this(new ToolHelpBatchAnalysisRunner())
    {
    }

    internal HelpBatchAnalysisCommandService(IHelpBatchAnalysisRunner runner)
    {
        _runner = runner;
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
        var snapshotLookup = LoadCurrentSnapshotLookup(root);
        var expectedItems = new JsonArray();
        var failures = new List<string>();

        Directory.CreateDirectory(downloadRoot);
        Directory.CreateDirectory(Path.Combine(downloadRoot, "plan"));

        foreach (var item in plan.Items)
        {
            var outcome = await RunItemAsync(
                downloadRoot,
                resolvedBatchId,
                source,
                item,
                timeouts,
                snapshotLookup,
                cancellationToken);
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
            ["selectedCount"] = plan.Items.Count,
            ["skippedCount"] = 0,
            ["items"] = expectedItems,
            ["skipped"] = new JsonArray(),
        });

        var output = ToolRuntime.CreateOutput();
        if (failures.Count > 0)
        {
            return await output.WriteErrorAsync(
                kind: "partial-failure",
                message: $"Help batch completed with {failures.Count} failure(s) out of {plan.Items.Count}. Expected plan: {expectedPath}. First failure: {failures[0]}",
                exitCode: 1,
                json: json,
                cancellationToken: cancellationToken);
        }

        return await output.WriteSuccessAsync(
            new
            {
                batchId = resolvedBatchId,
                selectedCount = plan.Items.Count,
                successCount = plan.Items.Count,
                expectedPlanPath = expectedPath,
            },
            [
                new SummaryRow("Batch", resolvedBatchId),
                new SummaryRow("Selected items", plan.Items.Count.ToString()),
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
        var artifactName = string.IsNullOrWhiteSpace(item.ArtifactName)
            ? BuildArtifactName(item.PackageId, item.Version)
            : item.ArtifactName;
        var itemOutputRoot = Path.Combine(downloadRoot, artifactName);
        var exitCode = await _runner.RunAsync(item, itemOutputRoot, batchId, source, timeouts, cancellationToken);
        var resultPath = Path.Combine(itemOutputRoot, "result.json");
        var result = File.Exists(resultPath)
            ? JsonNode.Parse(await File.ReadAllTextAsync(resultPath, cancellationToken))?.AsObject()
            : null;
        var openCliArtifactName = result?["artifacts"]?["opencliArtifact"]?.GetValue<string>();
        var openCliExists = !string.IsNullOrWhiteSpace(openCliArtifactName) &&
                            File.Exists(Path.Combine(itemOutputRoot, openCliArtifactName));
        var disposition = result?["disposition"]?.GetValue<string>();
        var success = exitCode == 0 &&
                      string.Equals(disposition, "success", StringComparison.Ordinal) &&
                      openCliExists;
        var snapshot = snapshotLookup.TryGetValue(item.PackageId, out var value) ? value : null;

        return new HelpBatchItemOutcome(
            Success: success,
            FailureSummary: BuildFailureSummary(item, result, disposition, openCliExists),
            ExpectedItem: BuildExpectedItem(item, artifactName, result, snapshot));
    }

    private static JsonObject BuildExpectedItem(
        HelpBatchItem item,
        string artifactName,
        JsonObject? result,
        HelpBatchSnapshotItem? snapshot)
    {
        var expectedItem = new JsonObject
        {
            ["packageId"] = item.PackageId,
            ["version"] = item.Version,
            ["attempt"] = item.Attempt,
            ["command"] = item.CommandName,
            ["artifactName"] = artifactName,
            ["packageUrl"] = FirstNonEmpty(
                result?["packageUrl"]?.GetValue<string>(),
                item.PackageUrl,
                snapshot?.PackageUrl,
                $"https://www.nuget.org/packages/{item.PackageId}/{item.Version}"),
            ["packageContentUrl"] = FirstNonEmpty(
                result?["packageContentUrl"]?.GetValue<string>(),
                item.PackageContentUrl,
                snapshot?.PackageContentUrl),
            ["catalogEntryUrl"] = FirstNonEmpty(
                result?["catalogEntryUrl"]?.GetValue<string>(),
                item.CatalogEntryUrl,
                snapshot?.CatalogEntryUrl),
            ["totalDownloads"] = result?["totalDownloads"]?.GetValue<long?>() ?? item.TotalDownloads ?? snapshot?.TotalDownloads,
        };

        SetOptionalString(expectedItem, "cliFramework", item.CliFramework ?? result?["cliFramework"]?.GetValue<string>());
        return expectedItem;
    }

    private static string BuildFailureSummary(HelpBatchItem item, JsonObject? result, string? disposition, bool openCliExists)
    {
        var failureMessage = result?["failureMessage"]?.GetValue<string>();
        if (!string.Equals(disposition, "success", StringComparison.Ordinal))
        {
            return $"{item.PackageId} {item.Version}: {disposition ?? "missing-result"} {failureMessage ?? "No failure message was recorded."}";
        }

        return openCliExists
            ? $"{item.PackageId} {item.Version}: analysis runner did not report success."
            : $"{item.PackageId} {item.Version}: success result is missing opencli artifact.";
    }

    private static IReadOnlyDictionary<string, HelpBatchSnapshotItem> LoadCurrentSnapshotLookup(string repositoryRoot)
    {
        var snapshotPath = Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json");
        if (!File.Exists(snapshotPath))
        {
            return new Dictionary<string, HelpBatchSnapshotItem>(StringComparer.OrdinalIgnoreCase);
        }

        var snapshot = JsonNode.Parse(File.ReadAllText(snapshotPath))?.AsObject();
        var packages = snapshot?["packages"]?.AsArray();
        if (packages is null)
        {
            return new Dictionary<string, HelpBatchSnapshotItem>(StringComparer.OrdinalIgnoreCase);
        }

        return packages
            .OfType<JsonObject>()
            .Select(package => new HelpBatchSnapshotItem(
                PackageId: package["packageId"]?.GetValue<string>() ?? string.Empty,
                TotalDownloads: package["totalDownloads"]?.GetValue<long?>(),
                PackageUrl: package["packageUrl"]?.GetValue<string>(),
                PackageContentUrl: package["packageContentUrl"]?.GetValue<string>(),
                CatalogEntryUrl: package["catalogEntryUrl"]?.GetValue<string>()))
            .Where(package => !string.IsNullOrWhiteSpace(package.PackageId))
            .ToDictionary(package => package.PackageId, StringComparer.OrdinalIgnoreCase);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string BuildArtifactName(string packageId, string version)
        => $"analysis-{packageId.ToLowerInvariant()}-{version.ToLowerInvariant()}";

    private static void SetOptionalString(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[propertyName] = value;
        }
    }

    private sealed record HelpBatchItemOutcome(bool Success, string FailureSummary, JsonObject ExpectedItem);
    private sealed record HelpBatchSnapshotItem(string PackageId, long? TotalDownloads, string? PackageUrl, string? PackageContentUrl, string? CatalogEntryUrl);
}

internal interface IHelpBatchAnalysisRunner
{
    Task<int> RunAsync(
        HelpBatchItem item,
        string outputRoot,
        string batchId,
        string source,
        HelpBatchTimeouts timeouts,
        CancellationToken cancellationToken);
}

internal sealed class ToolHelpBatchAnalysisRunner : IHelpBatchAnalysisRunner
{
    private readonly ToolHelpAnalysisService _service = new();

    public Task<int> RunAsync(
        HelpBatchItem item,
        string outputRoot,
        string batchId,
        string source,
        HelpBatchTimeouts timeouts,
        CancellationToken cancellationToken)
        => _service.RunQuietAsync(
            item.PackageId,
            item.Version,
            item.CommandName,
            outputRoot,
            batchId,
            item.Attempt,
            source,
            item.CliFramework,
            timeouts.InstallTimeoutSeconds,
            timeouts.AnalysisTimeoutSeconds,
            timeouts.CommandTimeoutSeconds,
            cancellationToken);
}
