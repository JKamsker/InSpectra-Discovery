namespace InSpectra.Discovery.Tool.Analysis;

using Spectre.Console.Cli;

internal abstract class NonSpectrePackageAnalysisSettingsBase : PackageAnalysisSettingsBase
{
    [CommandOption("--command|--tool-command <NAME>")]
    public string? Command { get; set; }
}
