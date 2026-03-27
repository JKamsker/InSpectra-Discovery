using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class DocsFullyIndexedReportCommand : AsyncCommand<DocsFullyIndexedReportCommand.Settings>
{
    private readonly DocsCommandService _service = new();

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--manifest <PATH>")]
        [DefaultValue("index/all.json")]
        public string ManifestPath { get; set; } = "index/all.json";

        [CommandOption("--output <PATH>")]
        [DefaultValue("docs/Reports/fully-indexed-documentation-report.md")]
        public string OutputPath { get; set; } = "docs/Reports/fully-indexed-documentation-report.md";

        public override ValidationResult Validate() => ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.BuildFullyIndexedDocumentationReportAsync(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot(),
            settings.ManifestPath,
            settings.OutputPath,
            settings.Json,
            cancellationToken);
}
