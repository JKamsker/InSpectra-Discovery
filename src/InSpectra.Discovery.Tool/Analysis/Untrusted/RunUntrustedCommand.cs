namespace InSpectra.Discovery.Tool.Analysis.Untrusted;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class RunUntrustedCommand : AsyncCommand<RunUntrustedCommand.Settings>
{
    private readonly UntrustedCommandService _service;

    public RunUntrustedCommand(UntrustedCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : PackageAnalysisSettingsBase
    {
        [CommandOption("--source <NAME>")]
        [DefaultValue("untrusted-batch")]
        public string Source { get; set; } = "untrusted-batch";

        [CommandOption("--install-timeout-seconds <NUMBER>")]
        [DefaultValue(300)]
        public int InstallTimeoutSeconds { get; set; } = 300;

        [CommandOption("--command-timeout-seconds <NUMBER>")]
        [DefaultValue(60)]
        public int CommandTimeoutSeconds { get; set; } = 60;

        public override ValidationResult Validate()
            => ValidatePackageAnalysis(InstallTimeoutSeconds, CommandTimeoutSeconds);
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RunUntrustedAsync(
            settings.PackageId,
            settings.Version,
            settings.OutputRoot,
            settings.BatchId,
            settings.Attempt,
            settings.Source,
            settings.InstallTimeoutSeconds,
            settings.CommandTimeoutSeconds,
            settings.Json,
            cancellationToken);
}



