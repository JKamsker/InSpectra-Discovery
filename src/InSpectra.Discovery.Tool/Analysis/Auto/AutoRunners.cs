namespace InSpectra.Discovery.Tool.Analysis.Auto;

using InSpectra.Discovery.Tool.Analysis.CliFx;
using InSpectra.Discovery.Tool.Analysis.Help;
using InSpectra.Discovery.Tool.Analysis.Static;
using InSpectra.Discovery.Tool.Analysis.Untrusted;

internal interface IAutoNativeRunner
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

internal interface IAutoHelpRunner
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

internal interface IAutoCliFxRunner
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

internal interface IAutoStaticRunner
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

internal sealed class AutoNativeRunnerAdapter : IAutoNativeRunner
{
    private readonly UntrustedCommandService _service = new();

    public async Task RunAsync(string packageId, string version, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        => await _service.RunQuietAsync(packageId, version, outputRoot, batchId, attempt, source, installTimeoutSeconds, commandTimeoutSeconds, cancellationToken);
}

internal sealed class AutoHelpRunnerAdapter : IAutoHelpRunner
{
    private readonly ToolHelpService _service = new();

    public async Task RunAsync(string packageId, string version, string? commandName, string outputRoot, string batchId, int attempt, string source, string? cliFramework, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        => await _service.RunQuietAsync(packageId, version, commandName, outputRoot, batchId, attempt, source, cliFramework, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, cancellationToken);
}

internal sealed class AutoCliFxRunnerAdapter : IAutoCliFxRunner
{
    private readonly CliFxService _service = new();

    public async Task RunAsync(string packageId, string version, string? commandName, string? cliFramework, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        => await _service.RunQuietAsync(packageId, version, commandName, cliFramework, outputRoot, batchId, attempt, source, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, cancellationToken);
}

internal sealed class AutoStaticRunnerAdapter : IAutoStaticRunner
{
    private readonly StaticService _service = new();

    public async Task RunAsync(string packageId, string version, string? commandName, string? cliFramework, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        => await _service.RunQuietAsync(packageId, version, commandName, cliFramework, outputRoot, batchId, attempt, source, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, cancellationToken);
}
