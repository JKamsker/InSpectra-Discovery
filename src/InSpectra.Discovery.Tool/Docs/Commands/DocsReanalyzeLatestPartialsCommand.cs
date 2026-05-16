namespace InSpectra.Discovery.Tool.Docs.Commands;

using InSpectra.Discovery.Tool.Docs.Services;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Infrastructure.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class DocsReanalyzeLatestPartialsCommand : AsyncCommand<DocsReanalyzeLatestPartialsCommand.Settings>
{
    private readonly DocsPartialReanalysisCommandService _service;

    public DocsReanalyzeLatestPartialsCommand(DocsPartialReanalysisCommandService service)
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

        [CommandOption("--source <SOURCE>")]
        [DefaultValue("docs-reanalyze-latest-partials")]
        public string Source { get; set; } = "docs-reanalyze-latest-partials";

        [CommandOption("--working-root <PATH>")]
        public string? WorkingRoot { get; set; }

        [CommandOption("--keep-working-root")]
        [DefaultValue(false)]
        public bool KeepWorkingRoot { get; set; }

        [CommandOption("--install-timeout-seconds <SECONDS>")]
        [DefaultValue(120)]
        public int InstallTimeoutSeconds { get; set; } = 120;

        [CommandOption("--analysis-timeout-seconds <SECONDS>")]
        [DefaultValue(180)]
        public int AnalysisTimeoutSeconds { get; set; } = 180;

        [CommandOption("--command-timeout-seconds <SECONDS>")]
        [DefaultValue(30)]
        public int CommandTimeoutSeconds { get; set; } = 30;

        public override ValidationResult Validate()
            => !string.IsNullOrWhiteSpace(Version) && string.IsNullOrWhiteSpace(PackageId)
                ? ValidationResult.Error("`--version` requires `--package-id`.")
                : Limit is <= 0
                    || InstallTimeoutSeconds <= 0
                    || AnalysisTimeoutSeconds <= 0
                    || CommandTimeoutSeconds <= 0
                    ? ValidationResult.Error("`--limit` and timeout values must be positive when provided.")
                    : ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.ReanalyzeLatestPartialsAsync(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot(),
            new LatestPartialMetadataSelectionCriteria(
                settings.PackageId,
                settings.Version,
                settings.AnalysisMode,
                settings.Classification,
                settings.MessageContains,
                settings.Limit),
            settings.BatchId,
            settings.Source,
            settings.WorkingRoot,
            settings.KeepWorkingRoot,
            settings.InstallTimeoutSeconds,
            settings.AnalysisTimeoutSeconds,
            settings.CommandTimeoutSeconds,
            settings.Json,
            cancellationToken);
}
