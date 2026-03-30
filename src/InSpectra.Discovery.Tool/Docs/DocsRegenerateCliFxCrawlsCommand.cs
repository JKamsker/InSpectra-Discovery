internal sealed class DocsRegenerateCliFxCrawlsCommand : DocsArtifactRegenerationCommandBase
{
    private readonly CliFxCrawlArtifactRegenerator _regenerator = new();

    protected override string ArtifactLabel => "CliFx crawl artifacts";

    protected override ArtifactRegenerationCommandResult Regenerate(string repositoryRoot, ArtifactRegenerationScope scope, bool rebuildIndexes)
    {
        var result = _regenerator.RegenerateRepository(repositoryRoot, scope, rebuildIndexes);
        return new ArtifactRegenerationCommandResult(
            result.ScannedCount,
            result.CandidateCount,
            result.RewrittenCount,
            result.UnchangedCount,
            result.FailedCount,
            result.RewrittenItems,
            result.FailedItems);
    }
}
