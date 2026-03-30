using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class AnalysisRunHelpBatchCommand : AsyncCommand<AnalysisRunHelpBatchCommand.Settings>
{
    private readonly HelpBatchAnalysisCommandService _service = new();

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--plan <PATH>")]
        public string PlanPath { get; set; } = string.Empty;

        [CommandOption("--output-root|--output|--out <PATH>")]
        public string OutputRoot { get; set; } = string.Empty;

        [CommandOption("--batch-id|--batch <ID>")]
        public string? BatchId { get; set; }

        [CommandOption("--source <NAME>")]
        [DefaultValue("help-batch")]
        public string Source { get; set; } = "help-batch";

        [CommandOption("--target-branch <NAME>")]
        [DefaultValue("main")]
        public string TargetBranch { get; set; } = "main";

        [CommandOption("--install-timeout-seconds <NUMBER>")]
        [DefaultValue(300)]
        public int InstallTimeoutSeconds { get; set; } = 300;

        [CommandOption("--analysis-timeout-seconds <NUMBER>")]
        [DefaultValue(600)]
        public int AnalysisTimeoutSeconds { get; set; } = 600;

        [CommandOption("--command-timeout-seconds <NUMBER>")]
        [DefaultValue(60)]
        public int CommandTimeoutSeconds { get; set; } = 60;

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(PlanPath)
               || string.IsNullOrWhiteSpace(OutputRoot)
               || InstallTimeoutSeconds <= 0
               || AnalysisTimeoutSeconds <= 0
               || CommandTimeoutSeconds <= 0
                ? ValidationResult.Error("`--plan`, `--output-root`, and positive timeout values are required.")
                : ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RunAsync(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot(),
            settings.PlanPath,
            settings.OutputRoot,
            settings.BatchId,
            settings.Source,
            settings.TargetBranch,
            settings.InstallTimeoutSeconds,
            settings.AnalysisTimeoutSeconds,
            settings.CommandTimeoutSeconds,
            settings.Json,
            cancellationToken);
}
