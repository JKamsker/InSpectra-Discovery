namespace InSpectra.Discovery.Tool.Docs.Commands;

using InSpectra.Discovery.Tool.Docs.Services;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Infrastructure.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class DocsGitHubPagesSnapshotCommand : AsyncCommand<DocsGitHubPagesSnapshotCommand.Settings>
{
    private readonly DocsCommandService _service;

    public DocsGitHubPagesSnapshotCommand(DocsCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--source-root <PATH>")]
        [DefaultValue("index")]
        public string SourceRoot { get; set; } = "index";

        [CommandOption("--output-root <PATH>")]
        [DefaultValue("artifacts/github-pages")]
        public string OutputRoot { get; set; } = "artifacts/github-pages";

        public override ValidationResult Validate() => ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.BuildGitHubPagesSnapshotAsync(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot(),
            settings.SourceRoot,
            settings.OutputRoot,
            settings.Json,
            cancellationToken);
}
