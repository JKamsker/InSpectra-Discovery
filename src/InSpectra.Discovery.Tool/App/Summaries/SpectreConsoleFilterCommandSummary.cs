namespace InSpectra.Discovery.Tool.App.Summaries;


internal sealed record SpectreConsoleFilterCommandSummary(
    string Command,
    string InputPath,
    string OutputPath,
    int ScannedPackageCount,
    int MatchedPackageCount);
