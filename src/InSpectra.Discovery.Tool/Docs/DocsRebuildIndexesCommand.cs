using Spectre.Console;
using Spectre.Console.Cli;
internal sealed class DocsRebuildIndexesCommand : AsyncCommand<DocsRebuildIndexesCommand.Settings>
{
    private readonly DocsCommandService _service = new();

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
