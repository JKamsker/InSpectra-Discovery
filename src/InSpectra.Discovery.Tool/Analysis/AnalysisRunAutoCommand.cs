using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class AnalysisRunAutoCommand : AsyncCommand<AnalysisRunAutoCommand.Settings>
{
    private readonly AutoAnalysisCommandService _service = new();

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--package-id <ID>")]
        public string PackageId { get; set; } = string.Empty;

        [CommandOption("--version <VERSION>")]
        public string Version { get; set; } = string.Empty;

        [CommandOption("--output-root <PATH>")]
        public string OutputRoot { get; set; } = string.Empty;

        [CommandOption("--batch-id <ID>")]
        public string BatchId { get; set; } = string.Empty;

        [CommandOption("--attempt <NUMBER>")]
        [DefaultValue(1)]
        public int Attempt { get; set; } = 1;

        [CommandOption("--source <NAME>")]
        [DefaultValue("auto-analysis")]
        public string Source { get; set; } = "auto-analysis";

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
            => string.IsNullOrWhiteSpace(PackageId)
                || string.IsNullOrWhiteSpace(Version)
                || string.IsNullOrWhiteSpace(OutputRoot)
                || string.IsNullOrWhiteSpace(BatchId)
                || Attempt <= 0
                || InstallTimeoutSeconds <= 0
                || AnalysisTimeoutSeconds <= 0
                || CommandTimeoutSeconds <= 0
                ? ValidationResult.Error("`--package-id`, `--version`, `--output-root`, `--batch-id`, and positive timeout/attempt values are required.")
                : ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RunAsync(
            settings.PackageId,
            settings.Version,
            settings.OutputRoot,
            settings.BatchId,
            settings.Attempt,
            settings.Source,
            settings.InstallTimeoutSeconds,
            settings.AnalysisTimeoutSeconds,
            settings.CommandTimeoutSeconds,
            settings.Json,
            cancellationToken);
}
