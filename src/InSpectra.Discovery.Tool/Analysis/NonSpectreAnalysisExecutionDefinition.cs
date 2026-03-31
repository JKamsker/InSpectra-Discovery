namespace InSpectra.Discovery.Tool.Analysis;

internal sealed record NonSpectreAnalysisExecutionDefinition(
    string AnalysisMode,
    string TempRootPrefix,
    string TimeoutLabel,
    string? DefaultCliFramework = null,
    bool InitializeCoverage = false);
