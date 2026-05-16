namespace InSpectra.Discovery.Tool.Analysis.Auto.Runners;

using InSpectra.Discovery.Tool.Analysis.Bridge;
using InSpectra.Discovery.Tool.Analysis.Help.Services;

internal sealed class AutoHelpRunnerAdapter : IAutoHelpRunner
{
    private readonly HelpService _service;

    public AutoHelpRunnerAdapter(LibAnalysisBridge bridge)
    {
        _service = new HelpService(bridge);
    }

    public async Task RunAsync(
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
        CancellationToken cancellationToken)
        => await _service.RunQuietAsync(
            packageId,
            version,
            commandName,
            outputRoot,
            batchId,
            attempt,
            source,
            cliFramework,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            cancellationToken);
}
