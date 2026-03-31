namespace InSpectra.Discovery.Tool.Analysis.Help.Models;


internal sealed record HelpBatchTimeouts(
    int InstallTimeoutSeconds,
    int AnalysisTimeoutSeconds,
    int CommandTimeoutSeconds);
