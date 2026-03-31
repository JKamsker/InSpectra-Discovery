namespace InSpectra.Discovery.Tool.Analysis.Auto.Runners;

using InSpectra.Discovery.Tool.Analysis.Hook;

internal sealed class AutoHookRunnerAdapter : IAutoHookRunner
{
    private readonly HookService _service = new();

    public async Task RunAsync(
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
        CancellationToken cancellationToken)
        => await _service.RunQuietAsync(
            packageId,
            version,
            commandName,
            cliFramework,
            outputRoot,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            cancellationToken);
}
