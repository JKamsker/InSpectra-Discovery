namespace InSpectra.Discovery.Tool.Docs.Services;

using InSpectra.Discovery.Tool.Analysis.Auto.Services;
using InSpectra.Discovery.Tool.App.Machine;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Promotion.Services;

using Spectre.Console;
using System.Text.Json.Nodes;

internal sealed class DocsPartialReanalysisCommandService
{
    private readonly AutoCommandService _autoCommandService;
    private readonly PromotionApplyCommandService _promotionApplyCommandService;

    public DocsPartialReanalysisCommandService()
        : this(
            new AutoCommandService(),
            new PromotionApplyCommandService())
    {
    }

    internal DocsPartialReanalysisCommandService(
        AutoCommandService autoCommandService,
        PromotionApplyCommandService promotionApplyCommandService)
    {
        _autoCommandService = autoCommandService;
        _promotionApplyCommandService = promotionApplyCommandService;
    }

    public async Task<int> ReanalyzeLatestPartialsAsync(
        string repositoryRoot,
        LatestPartialMetadataSelectionCriteria criteria,
        string? batchId,
        string source,
        string? workingRoot,
        bool keepWorkingRoot,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        CancellationToken cancellationToken)
    {
        var selected = LatestPartialMetadataSelectionSupport.Select(repositoryRoot, criteria);
        var output = Runtime.CreateOutput();
        if (selected.Count == 0)
        {
            return await output.WriteSuccessAsync(
                new
                {
                    batchId,
                    selectedCount = 0,
                    workingRoot = string.IsNullOrWhiteSpace(workingRoot) ? null : Path.GetFullPath(workingRoot),
                },
                [
                    new SummaryRow("Batch", batchId ?? string.Empty),
                    new SummaryRow("Selected partials", "0"),
                ],
                json,
                cancellationToken);
        }

        var resolvedBatchId = string.IsNullOrWhiteSpace(batchId)
            ? $"docs-reanalyze-partials-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}"
            : batchId;
        var resolvedWorkingRoot = ResolveWorkingRoot(workingRoot);
        var preserveWorkingRoot = keepWorkingRoot || !string.IsNullOrWhiteSpace(workingRoot);
        var expectedPath = Path.Combine(resolvedWorkingRoot, "plan", "expected.json");
        var summaryPath = Path.Combine(resolvedWorkingRoot, "promotion-summary.json");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);
            Directory.CreateDirectory(Path.Combine(resolvedWorkingRoot, "results"));
            WriteExpectedPlan(expectedPath, resolvedBatchId, selected);

            for (var i = 0; i < selected.Count; i++)
            {
                var item = selected[i];
                var itemOutputRoot = Path.Combine(
                    resolvedWorkingRoot,
                    "results",
                    $"{i:D4}-{NormalizeSegment(item.PackageId)}-{NormalizeSegment(item.Version)}");
                await _autoCommandService.RunQuietAsync(
                    item.PackageId,
                    item.Version,
                    itemOutputRoot,
                    resolvedBatchId,
                    item.NextAttempt,
                    source,
                    installTimeoutSeconds,
                    analysisTimeoutSeconds,
                    commandTimeoutSeconds,
                    cancellationToken);
            }

            var applyResult = await _promotionApplyCommandService.ApplyUntrustedQuietAsync(
                resolvedWorkingRoot,
                summaryPath,
                cancellationToken);
            var summary = applyResult.Summary;

            return await output.WriteSuccessAsync(
                new
                {
                    batchId = resolvedBatchId,
                    selectedCount = selected.Count,
                    workingRoot = preserveWorkingRoot ? resolvedWorkingRoot : null,
                    successCount = summary["successCount"]?.GetValue<int>() ?? 0,
                    terminalNegativeCount = summary["terminalNegativeCount"]?.GetValue<int>() ?? 0,
                    retryableFailureCount = summary["retryableFailureCount"]?.GetValue<int>() ?? 0,
                    terminalFailureCount = summary["terminalFailureCount"]?.GetValue<int>() ?? 0,
                    missingCount = summary["missingCount"]?.GetValue<int>() ?? 0,
                    summaryOutputPath = preserveWorkingRoot ? applyResult.SummaryOutputPath : null,
                },
                [
                    new SummaryRow("Batch", resolvedBatchId),
                    new SummaryRow("Selected partials", selected.Count.ToString()),
                    new SummaryRow("Success", (summary["successCount"]?.GetValue<int>() ?? 0).ToString()),
                    new SummaryRow("Terminal negative", (summary["terminalNegativeCount"]?.GetValue<int>() ?? 0).ToString()),
                    new SummaryRow("Retryable failure", (summary["retryableFailureCount"]?.GetValue<int>() ?? 0).ToString()),
                    new SummaryRow("Terminal failure", (summary["terminalFailureCount"]?.GetValue<int>() ?? 0).ToString()),
                    new SummaryRow("Missing artifacts", (summary["missingCount"]?.GetValue<int>() ?? 0).ToString()),
                    new SummaryRow("Working root", preserveWorkingRoot ? resolvedWorkingRoot : "(temporary)"),
                ],
                json,
                cancellationToken);
        }
        finally
        {
            if (!preserveWorkingRoot && Directory.Exists(resolvedWorkingRoot))
            {
                Directory.Delete(resolvedWorkingRoot, recursive: true);
            }
        }
    }

    private static void WriteExpectedPlan(
        string expectedPath,
        string batchId,
        IReadOnlyList<LatestPartialMetadataSelection> items)
    {
        var expected = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["batchId"] = batchId,
            ["targetBranch"] = "main",
            ["items"] = new JsonArray(items
                .Select(item => new JsonObject
                {
                    ["packageId"] = item.PackageId,
                    ["version"] = item.Version,
                    ["attempt"] = item.NextAttempt,
                })
                .ToArray()),
        };

        RepositoryPathResolver.WriteJsonFile(expectedPath, expected);
    }

    private static string ResolveWorkingRoot(string? workingRoot)
        => string.IsNullOrWhiteSpace(workingRoot)
            ? Path.Combine(Path.GetTempPath(), $"inspectra-reanalyze-partials-{Guid.NewGuid():N}")
            : Path.GetFullPath(workingRoot);

    private static string NormalizeSegment(string value)
    {
        var normalized = new string(value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '-')
            .ToArray())
            .Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(normalized) ? "item" : normalized;
    }
}
