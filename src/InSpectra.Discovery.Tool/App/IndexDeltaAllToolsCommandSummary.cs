namespace InSpectra.Discovery.Tool.App;

internal sealed record IndexDeltaAllToolsCommandSummary(
    string Command,
    string InputDeltaPath,
    string OutputDeltaPath,
    string QueueOutputPath,
    int ScannedChangeCount,
    int MatchedPackageCount,
    int QueueCount);
