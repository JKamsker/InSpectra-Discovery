namespace InSpectra.Discovery.Tool.Docs.Commands;

using InSpectra.Discovery.Tool.Docs.Services;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Infrastructure.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class DocsExportLatestPartialsPlanCommand : AsyncCommand<DocsExportLatestPartialsPlanCommand.Settings>
{
    private readonly DocsPartialPlanCommandService _service;

    public DocsExportLatestPartialsPlanCommand(DocsPartialPlanCommandService service)
    {
        _service = service;
    }

    internal sealed class Settings : GlobalSettings
    {
        [CommandOption("--package-id|--package <PACKAGE_ID>")]
        public string? PackageId { get; set; }

        [CommandOption("--version <VERSION>")]
        public string? Version { get; set; }

        [CommandOption("--analysis-mode <MODE>")]
        public string? AnalysisMode { get; set; }

        [CommandOption("--classification <CLASSIFICATION>")]
        public string? Classification { get; set; }

        [CommandOption("--message-contains <TEXT>")]
        public string? MessageContains { get; set; }

        [CommandOption("--limit <COUNT>")]
        public int? Limit { get; set; }

        [CommandOption("--batch-id|--batch <ID>")]
        public string? BatchId { get; set; }

        [CommandOption("--output|--expected-path <PATH>")]
        public string OutputPath { get; set; } = string.Empty;

        [CommandOption("--target-branch <NAME>")]
        [DefaultValue("main")]
        public string TargetBranch { get; set; } = "main";

        public override ValidationResult Validate()
            => !string.IsNullOrWhiteSpace(Version) && string.IsNullOrWhiteSpace(PackageId)
                ? ValidationResult.Error("`--version` requires `--package-id`.")
                : Limit is <= 0
                    ? ValidationResult.Error("`--limit` must be positive when provided.")
                    : string.IsNullOrWhiteSpace(OutputPath)
                        ? ValidationResult.Error("`--output` is required.")
                        : ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.ExportLatestPartialsPlanAsync(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot(),
            new LatestPartialMetadataSelectionCriteria(
                settings.PackageId,
                settings.Version,
                settings.AnalysisMode,
                settings.Classification,
                settings.MessageContains,
                settings.Limit),
            settings.BatchId,
            settings.OutputPath,
            settings.TargetBranch,
            settings.Json,
            cancellationToken);
}
