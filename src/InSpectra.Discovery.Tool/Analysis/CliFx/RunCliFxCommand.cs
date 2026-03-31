namespace InSpectra.Discovery.Tool.Analysis.CliFx;

using InSpectra.Discovery.Tool.Analysis.NonSpectre;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class RunCliFxCommand : AsyncCommand<RunCliFxCommand.Settings>
{
    private readonly CliFxService _service;

    public RunCliFxCommand(CliFxService service)
    {
        _service = service;
    }

    public sealed class Settings : NonSpectrePackageAnalysisSettingsBase
    {
        [CommandOption("--source <NAME>")]
        [DefaultValue("clifx-help-crawl")]
        public string Source { get; set; } = "clifx-help-crawl";

        [CommandOption("--install-timeout-seconds <NUMBER>")]
        [DefaultValue(300)]
        public int InstallTimeoutSeconds { get; set; } = 300;

        [CommandOption("--analysis-timeout-seconds <NUMBER>")]
        [DefaultValue(600)]
        public int AnalysisTimeoutSeconds { get; set; } = 600;

        [CommandOption("--command-timeout-seconds <NUMBER>")]
        [DefaultValue(30)]
        public int CommandTimeoutSeconds { get; set; } = 30;

        public override ValidationResult Validate()
            => ValidatePackageAnalysis(InstallTimeoutSeconds, AnalysisTimeoutSeconds, CommandTimeoutSeconds);
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RunAsync(
            settings.PackageId,
            settings.Version,
            settings.Command,
            null,
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



