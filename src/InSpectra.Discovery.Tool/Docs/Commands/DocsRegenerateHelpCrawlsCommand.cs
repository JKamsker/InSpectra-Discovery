namespace InSpectra.Discovery.Tool.Docs.Commands;

using InSpectra.Discovery.Tool.Docs.Services;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

internal sealed class DocsRegenerateHelpCrawlsCommand : DocsArtifactRegenerationCommandBase
{
    private readonly HelpCrawlArtifactRegenerationService _service;

    public DocsRegenerateHelpCrawlsCommand(HelpCrawlArtifactRegenerationService service)
    {
        _service = service;
    }

    protected override string ArtifactLabel => "Help-crawl artifacts";

    protected override ArtifactRegenerationRunResult Regenerate(string repositoryRoot, ArtifactRegenerationScope scope, bool rebuildIndexes)
        => _service.RegenerateRepository(repositoryRoot, scope, rebuildIndexes);
}
