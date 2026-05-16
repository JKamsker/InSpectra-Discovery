internal sealed record IndexBuildCommandSummary(
    string Command,
    string OutputPath,
    int PackageCount,
    string SortOrder);

internal sealed record SpectreConsoleFilterCommandSummary(
    string Command,
    string InputPath,
    string OutputPath,
    int ScannedPackageCount,
    int MatchedPackageCount);
