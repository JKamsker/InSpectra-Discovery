namespace InSpectra.Discovery.Tool.Analysis.Auto.Commands;

using InSpectra.Discovery.Tool.Analysis.Auto.Services;

using InSpectra.Discovery.Tool.Analysis.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class RunAutoCommand : AsyncCommand<RunAutoCommand.Settings>
{
    private readonly AutoCommandService _service;

    public RunAutoCommand(AutoCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : PackageAnalysisSettingsBase
    {
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
            => ValidatePackageAnalysis(InstallTimeoutSeconds, AnalysisTimeoutSeconds, CommandTimeoutSeconds);
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



