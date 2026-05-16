namespace InSpectra.Discovery.Tool.Analysis.Help.Batch;

using InSpectra.Discovery.Tool.Analysis.Bridge;
using InSpectra.Discovery.Tool.Analysis.Help.Services;
using InSpectra.Discovery.Tool.Analysis.Help.Models;

internal sealed class HelpBatchRunner : IHelpBatchRunner
{
    private readonly HelpService _service;

    public HelpBatchRunner(LibAnalysisBridge bridge)
    {
        _service = new HelpService(bridge);
    }

    public Task<int> RunAsync(
        HelpBatchItem item,
        string outputRoot,
        string batchId,
        string source,
        HelpBatchTimeouts timeouts,
        CancellationToken cancellationToken)
        => _service.RunQuietAsync(
            item.PackageId,
            item.Version,
            item.CommandName,
            outputRoot,
            batchId,
            item.Attempt,
            source,
            item.CliFramework,
            timeouts.InstallTimeoutSeconds,
            timeouts.AnalysisTimeoutSeconds,
            timeouts.CommandTimeoutSeconds,
            cancellationToken);
}
