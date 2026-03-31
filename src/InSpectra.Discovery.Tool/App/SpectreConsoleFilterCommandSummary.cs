namespace InSpectra.Discovery.Tool.App;

internal sealed record SpectreConsoleFilterCommandSummary(
    string Command,
    string InputPath,
    string OutputPath,
    int ScannedPackageCount,
    int MatchedPackageCount);
