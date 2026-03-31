namespace InSpectra.Discovery.Tool.App;

internal sealed record IndexBuildCommandSummary(
    string Command,
    string OutputPath,
    int PackageCount,
    string SortOrder);
