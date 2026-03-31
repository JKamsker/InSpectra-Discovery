namespace InSpectra.Discovery.Tool.Analysis.Help;

internal sealed record HelpBatchTimeouts(
    int InstallTimeoutSeconds,
    int AnalysisTimeoutSeconds,
    int CommandTimeoutSeconds);
