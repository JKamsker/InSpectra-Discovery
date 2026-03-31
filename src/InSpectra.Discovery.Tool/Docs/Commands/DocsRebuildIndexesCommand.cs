namespace InSpectra.Discovery.Tool.Docs.Commands;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.Docs.Services;

using InSpectra.Discovery.Tool.Infrastructure.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
internal sealed class DocsRebuildIndexesCommand : AsyncCommand<DocsRebuildIndexesCommand.Settings>
{
    private readonly DocsCommandService _service;

    public DocsRebuildIndexesCommand(DocsCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--skip-browser-index")]
        public bool SkipBrowserIndex { get; set; }

        public override ValidationResult Validate() => ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RebuildIndexesAsync(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot(),
            writeBrowserIndex: !settings.SkipBrowserIndex,
            settings.Json,
            cancellationToken);
}


