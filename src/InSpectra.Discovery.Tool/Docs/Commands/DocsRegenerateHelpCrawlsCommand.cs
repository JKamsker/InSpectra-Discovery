namespace InSpectra.Discovery.Tool.Docs.Commands;

using InSpectra.Discovery.Tool.Help.Artifacts;

using InSpectra.Discovery.Tool.OpenCli.Artifacts;

internal sealed class DocsRegenerateHelpCrawlsCommand : DocsArtifactRegenerationCommandBase
{
    private readonly CrawlArtifactRegenerator _regenerator = new();

    protected override string ArtifactLabel => "Help-crawl artifacts";

    protected override ArtifactRegenerationRunResult Regenerate(string repositoryRoot, ArtifactRegenerationScope scope, bool rebuildIndexes)
    {
        var result = _regenerator.RegenerateRepository(repositoryRoot, scope, rebuildIndexes);
        return new ArtifactRegenerationRunResult(
            result.ScannedCount,
            result.CandidateCount,
            result.RewrittenCount,
            result.UnchangedCount,
            result.FailedCount,
            result.RewrittenItems,
            result.FailedItems);
    }
}

