namespace InSpectra.Discovery.Tool.Analysis.Auto.Runners;

using InSpectra.Discovery.Tool.Analysis.Static;

internal sealed class AutoStaticRunnerAdapter : IAutoStaticRunner
{
    private readonly StaticService _service = new();

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
