namespace InSpectra.Discovery.Tool.Analysis;

using Spectre.Console.Cli;

internal abstract class CliFrameworkPackageAnalysisSettingsBase : NonSpectrePackageAnalysisSettingsBase
{
    [CommandOption("--cli-framework|--framework <NAME>")]
    public string? CliFramework { get; set; }
}
