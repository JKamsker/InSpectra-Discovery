namespace InSpectra.Discovery.Tool.Analysis.Help.Commands;

using InSpectra.Discovery.Tool.Analysis.Help.Services;

using InSpectra.Discovery.Tool.Analysis.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class RunHelpCommand : AsyncCommand<RunHelpCommand.Settings>
{
    private readonly HelpService _service;

    public RunHelpCommand(HelpService service)
    {
        _service = service;
    }

    public sealed class Settings : CliFrameworkPackageAnalysisSettingsBase
    {
        [CommandOption("--source <NAME>")]
        [DefaultValue("help-crawl")]
        public string Source { get; set; } = "help-crawl";

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
            settings.OutputRoot,
            settings.BatchId,
            settings.Attempt,
            settings.Source,
            settings.CliFramework,
            settings.InstallTimeoutSeconds,
            settings.AnalysisTimeoutSeconds,
            settings.CommandTimeoutSeconds,
            settings.Json,
            cancellationToken);
}



