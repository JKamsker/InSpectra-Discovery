namespace InSpectra.Discovery.Tool.Analysis.NonSpectre;

using InSpectra.Discovery.Tool.Analysis.Settings;

using Spectre.Console.Cli;

internal abstract class NonSpectrePackageAnalysisSettingsBase : PackageAnalysisSettingsBase
{
    [CommandOption("--command|--tool-command <NAME>")]
    public string? Command { get; set; }
}
