namespace InSpectra.Discovery.Tool.Analysis.Auto.Runners;

using InSpectra.Discovery.Tool.Analysis.Untrusted;

internal sealed class AutoNativeRunnerAdapter : IAutoNativeRunner
{
    private readonly UntrustedCommandService _service = new();

    public async Task RunAsync(
        string packageId,
        string version,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
        => await _service.RunQuietAsync(
            packageId,
            version,
            outputRoot,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            commandTimeoutSeconds,
            cancellationToken);
}
