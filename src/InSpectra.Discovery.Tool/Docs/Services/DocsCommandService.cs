namespace InSpectra.Discovery.Tool.Docs.Services;

using InSpectra.Discovery.Tool.Docs.Reports;

using InSpectra.Discovery.Tool.Docs.Indexing;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.App.Machine;

using InSpectra.Discovery.Tool.Infrastructure.Host;

using InSpectra.Discovery.Tool.Indexing;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

internal sealed class DocsCommandService
{
    public async Task<int> RebuildIndexesAsync(
        string repositoryRoot,
        bool writeBrowserIndex,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var result = RepositoryPackageIndexBuilder.Rebuild(root, writeBrowserIndex);
        var output = Runtime.CreateOutput();

        return await output.WriteSuccessAsync(
            new
            {
                packageCount = result.PackageCount,
                versionRecordCount = result.VersionRecordCount,
                allIndexPath = result.AllIndexPath,
                browserIndexPath = result.BrowserIndexPath,
            },
            [
                new SummaryRow("Packages", result.PackageCount.ToString()),
                new SummaryRow("Version records", result.VersionRecordCount.ToString()),
                new SummaryRow("All index", result.AllIndexPath),
                new SummaryRow("Browser index", result.BrowserIndexPath ?? "skipped"),
            ],
            json,
            cancellationToken);
    }

    public async Task<int> BuildBrowserIndexAsync(
        string repositoryRoot,
        string allIndexPath,
        string outputPath,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var allIndexFile = Path.GetFullPath(Path.Combine(root, allIndexPath));
        var outputFile = Path.GetFullPath(Path.Combine(root, outputPath));
        var allIndex = await JsonNodeFileLoader.TryLoadJsonObjectAsync(allIndexFile, cancellationToken)
            ?? throw new InvalidOperationException($"Manifest '{allIndexFile}' is empty.");
        var browserIndex = DocsBrowserIndexSupport.BuildBrowserIndex(allIndex, outputFile, cancellationToken);

        RepositoryPathResolver.WriteJsonFile(outputFile, browserIndex);
        var output = Runtime.CreateOutput();
        return await output.WriteSuccessAsync(
            browserIndex,
            [
                new SummaryRow("Packages", browserIndex.PackageCount.ToString()),
                new SummaryRow("Output", outputFile),
            ],
            json,
            cancellationToken);
    }

    public async Task<int> BuildGitHubPagesSnapshotAsync(
        string repositoryRoot,
        string sourceRoot,
        string outputRoot,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var sourceDirectory = Path.GetFullPath(Path.Combine(root, sourceRoot));
        var outputDirectory = Path.GetFullPath(Path.Combine(root, outputRoot));
        var snapshot = await DocsGitHubPagesSnapshotSupport.BuildAsync(sourceDirectory, outputDirectory, cancellationToken);
        var output = Runtime.CreateOutput();

        return await output.WriteSuccessAsync(
            new
            {
                sourceRoot = snapshot.SourceRoot,
                outputRoot = snapshot.OutputRoot,
                publishedFileCount = snapshot.PublishedFileCount,
            },
            [
                new SummaryRow("Source root", snapshot.SourceRoot),
                new SummaryRow("Output root", snapshot.OutputRoot),
                new SummaryRow("Published JSON files", snapshot.PublishedFileCount.ToString()),
            ],
            json,
            cancellationToken);
    }

    public async Task<int> BuildFullyIndexedDocumentationReportAsync(
        string repositoryRoot,
        string manifestPath,
        string outputPath,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var manifestFile = Path.GetFullPath(Path.Combine(root, manifestPath));
        var reportFile = Path.GetFullPath(Path.Combine(root, outputPath));
        var manifest = await JsonNodeFileLoader.TryLoadJsonObjectAsync(manifestFile, cancellationToken)
            ?? throw new InvalidOperationException($"Manifest '{manifestFile}' is empty.");
        var report = DocsDocumentationReportSupport.BuildReport(root, manifest, cancellationToken);

        RepositoryPathResolver.WriteLines(reportFile, report.Lines);
        var result = new
        {
            packageCount = report.PackageCount,
            fullyDocumentedCount = report.FullyDocumentedCount,
            incompleteCount = report.IncompleteCount,
            outputPath = reportFile,
        };

        var output = Runtime.CreateOutput();
        return await output.WriteSuccessAsync(
            result,
            [
                new SummaryRow("Packages in scope", report.PackageCount.ToString()),
                new SummaryRow("Fully documented", report.FullyDocumentedCount.ToString()),
                new SummaryRow("Incomplete", report.IncompleteCount.ToString()),
                new SummaryRow("Output", reportFile),
            ],
            json,
            cancellationToken);
    }
}
