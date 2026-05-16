namespace InSpectra.Discovery.Tool.Docs.Commands;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.Docs.Services;

using InSpectra.Discovery.Tool.Infrastructure.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class DocsBrowserIndexCommand : AsyncCommand<DocsBrowserIndexCommand.Settings>
{
    private readonly DocsCommandService _service;

    public DocsBrowserIndexCommand(DocsCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--input <PATH>")]
        [DefaultValue("index/all.json")]
        public string InputPath { get; set; } = "index/all.json";

        [CommandOption("--output <PATH>")]
        [DefaultValue("index/index.json")]
        public string OutputPath { get; set; } = "index/index.json";

        public override ValidationResult Validate() => ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.BuildBrowserIndexAsync(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot(),
            settings.InputPath,
            settings.OutputPath,
            settings.Json,
            cancellationToken);
}


