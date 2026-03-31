namespace InSpectra.Discovery.Tool.Analysis.Help;

internal sealed record HelpBatchItem(
    string PackageId,
    string Version,
    string? CommandName,
    string? CliFramework,
    string AnalysisMode,
    IReadOnlyList<string> ExpectedCommands,
    IReadOnlyList<string> ExpectedOptions,
    IReadOnlyList<string> ExpectedArguments,
    int Attempt,
    string? ArtifactName,
    string? PackageUrl,
    string? PackageContentUrl,
    string? CatalogEntryUrl,
    long? TotalDownloads);
