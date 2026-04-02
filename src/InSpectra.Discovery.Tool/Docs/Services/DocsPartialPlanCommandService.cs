namespace InSpectra.Discovery.Tool.Docs.Services;

using InSpectra.Discovery.Tool.App.Machine;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

internal sealed class DocsPartialPlanCommandService
{
    public async Task<int> ExportLatestPartialsPlanAsync(
        string repositoryRoot,
        LatestPartialMetadataSelectionCriteria criteria,
        string? batchId,
        string outputPath,
        string targetBranch,
        bool json,
        CancellationToken cancellationToken)
    {
        var selected = LatestPartialMetadataSelectionSupport.Select(repositoryRoot, criteria);
        var resolvedBatchId = string.IsNullOrWhiteSpace(batchId)
            ? $"docs-latest-partials-plan-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}"
            : batchId;
        var resolvedOutputPath = Path.GetFullPath(outputPath);

        LatestPartialMetadataPlanSupport.WriteExpectedPlan(
            resolvedOutputPath,
            resolvedBatchId,
            selected,
            string.IsNullOrWhiteSpace(targetBranch) ? "main" : targetBranch);

        var output = Runtime.CreateOutput();
        return await output.WriteSuccessAsync(
            new
            {
                batchId = resolvedBatchId,
                selectedCount = selected.Count,
                outputPath = resolvedOutputPath,
                targetBranch = string.IsNullOrWhiteSpace(targetBranch) ? "main" : targetBranch,
            },
            [
                new SummaryRow("Batch", resolvedBatchId),
                new SummaryRow("Selected partials", selected.Count.ToString()),
                new SummaryRow("Output", RepositoryPathResolver.GetRelativePath(repositoryRoot, resolvedOutputPath)),
            ],
            json,
            cancellationToken);
    }
}
