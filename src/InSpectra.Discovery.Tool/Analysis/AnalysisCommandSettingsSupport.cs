using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal abstract class PackageAnalysisSettingsBase : GlobalSettings
{
    [CommandOption("--package-id|--package <ID>")]
    public string PackageId { get; set; } = string.Empty;

    [CommandOption("--version <VERSION>")]
    public string Version { get; set; } = string.Empty;

    [CommandOption("--output-root|--output|--out <PATH>")]
    public string OutputRoot { get; set; } = string.Empty;

    [CommandOption("--batch-id|--batch <ID>")]
    public string BatchId { get; set; } = string.Empty;

    [CommandOption("--attempt <NUMBER>")]
    [DefaultValue(1)]
    public int Attempt { get; set; } = 1;

    protected ValidationResult ValidatePackageAnalysis(params int[] positiveValues)
        => string.IsNullOrWhiteSpace(PackageId)
            || string.IsNullOrWhiteSpace(Version)
            || string.IsNullOrWhiteSpace(OutputRoot)
            || string.IsNullOrWhiteSpace(BatchId)
            || Attempt <= 0
            || positiveValues.Any(value => value <= 0)
            ? ValidationResult.Error("`--package-id`, `--version`, `--output-root`, `--batch-id`, and positive timeout/attempt values are required.")
            : ValidationResult.Success();
}

internal abstract class NonSpectrePackageAnalysisSettingsBase : PackageAnalysisSettingsBase
{
    [CommandOption("--command|--tool-command <NAME>")]
    public string? Command { get; set; }
}

internal abstract class CliFrameworkPackageAnalysisSettingsBase : NonSpectrePackageAnalysisSettingsBase
{
    [CommandOption("--cli-framework|--framework <NAME>")]
    public string? CliFramework { get; set; }
}
