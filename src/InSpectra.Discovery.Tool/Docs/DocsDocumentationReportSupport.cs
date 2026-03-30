namespace InSpectra.Discovery.Tool.Docs;

using System.Text.Json.Nodes;

internal sealed record DocumentationReportBuildResult(
    int PackageCount,
    int FullyDocumentedCount,
    int IncompleteCount,
    IReadOnlyList<string> Lines);

internal static class DocsDocumentationReportSupport
{
    public static DocumentationReportBuildResult BuildReport(
        string repositoryRoot,
        JsonObject manifest,
        CancellationToken cancellationToken)
    {
        var rows = new List<ReportRow>();

        foreach (var packageNode in manifest["packages"]?.AsArray() ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (packageNode is not JsonObject package)
            {
                continue;
            }

            if (DocsDocumentationReportPackageSupport.TryCreateReportRow(repositoryRoot, package, out var row))
            {
                rows.Add(row);
            }
        }

        var sortedRows = rows
            .OrderBy(row => row.OverallComplete ? 1 : 0)
            .ThenBy(row => row.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var fullyDocumentedCount = sortedRows.Count(row => row.OverallComplete);
        var incompleteCount = sortedRows.Count - fullyDocumentedCount;

        return new DocumentationReportBuildResult(
            PackageCount: sortedRows.Count,
            FullyDocumentedCount: fullyDocumentedCount,
            IncompleteCount: incompleteCount,
            Lines: DocsDocumentationReportFormattingSupport.BuildDocumentationReport(sortedRows, fullyDocumentedCount, incompleteCount));
    }
}

