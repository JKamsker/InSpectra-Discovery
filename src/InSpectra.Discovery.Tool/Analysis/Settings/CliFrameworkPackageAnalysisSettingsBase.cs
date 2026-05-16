namespace InSpectra.Discovery.Tool.Analysis.Settings;

using InSpectra.Discovery.Tool.Analysis.NonSpectre;

using Spectre.Console.Cli;

internal abstract class CliFrameworkPackageAnalysisSettingsBase : NonSpectrePackageAnalysisSettingsBase
{
    [CommandOption("--cli-framework|--framework <NAME>")]
    public string? CliFramework { get; set; }
}
