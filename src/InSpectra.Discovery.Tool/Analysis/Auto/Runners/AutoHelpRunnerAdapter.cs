namespace InSpectra.Discovery.Tool.Analysis.Auto.Runners;

using InSpectra.Discovery.Tool.Analysis.Help.Services;

using InSpectra.Discovery.Tool.Analysis.Help;

internal sealed class AutoHelpRunnerAdapter : IAutoHelpRunner
{
    private readonly HelpService _service = new();

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
