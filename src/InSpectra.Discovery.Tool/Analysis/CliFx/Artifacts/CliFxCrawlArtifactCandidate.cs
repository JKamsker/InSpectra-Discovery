namespace InSpectra.Discovery.Tool.Analysis.CliFx.Artifacts;


internal sealed record CliFxCrawlArtifactCandidate(
    string PackageId,
    string Version,
    string CommandName,
    string? CliFramework,
    string MetadataPath,
    string CrawlPath,
    string OpenCliPath)
{
    public string DisplayName => $"{PackageId} {Version}";
}
