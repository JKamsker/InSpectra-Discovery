internal interface IAutoAnalysisNativeRunner
{
    Task RunAsync(
        string packageId,
        string version,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);
}

internal interface IAutoAnalysisHelpRunner
{
    Task RunAsync(
        string packageId,
        string version,
        string? commandName,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        string? cliFramework,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);
}

internal interface IAutoAnalysisCliFxRunner
{
    Task RunAsync(
        string packageId,
        string version,
        string? commandName,
        string? cliFramework,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);
}

internal interface IAutoAnalysisStaticRunner
{
    Task RunAsync(
        string packageId,
        string version,
        string? commandName,
        string? cliFramework,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);
}

internal sealed class AutoAnalysisNativeRunnerAdapter : IAutoAnalysisNativeRunner
{
    private readonly AnalysisCommandService _service = new();

    public async Task RunAsync(string packageId, string version, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        => await _service.RunQuietAsync(packageId, version, outputRoot, batchId, attempt, source, installTimeoutSeconds, commandTimeoutSeconds, cancellationToken);
}

internal sealed class AutoAnalysisHelpRunnerAdapter : IAutoAnalysisHelpRunner
{
    private readonly ToolHelpAnalysisService _service = new();

    public async Task RunAsync(string packageId, string version, string? commandName, string outputRoot, string batchId, int attempt, string source, string? cliFramework, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        => await _service.RunQuietAsync(packageId, version, commandName, outputRoot, batchId, attempt, source, cliFramework, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, cancellationToken);
}

internal sealed class AutoAnalysisCliFxRunnerAdapter : IAutoAnalysisCliFxRunner
{
    private readonly CliFxAnalysisService _service = new();

    public async Task RunAsync(string packageId, string version, string? commandName, string? cliFramework, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        => await _service.RunQuietAsync(packageId, version, commandName, cliFramework, outputRoot, batchId, attempt, source, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, cancellationToken);
}

internal sealed class AutoAnalysisStaticRunnerAdapter : IAutoAnalysisStaticRunner
{
    private readonly StaticAnalysisService _service = new();

    public async Task RunAsync(string packageId, string version, string? commandName, string? cliFramework, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        => await _service.RunQuietAsync(packageId, version, commandName, cliFramework, outputRoot, batchId, attempt, source, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, cancellationToken);
}
