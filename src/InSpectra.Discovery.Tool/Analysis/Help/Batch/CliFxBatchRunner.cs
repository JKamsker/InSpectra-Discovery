namespace InSpectra.Discovery.Tool.Analysis.Help.Batch;

using InSpectra.Discovery.Tool.Analysis.Bridge;
using InSpectra.Discovery.Tool.Analysis.Help.Models;
using InSpectra.Discovery.Tool.Analysis.CliFx;

internal sealed class CliFxBatchRunner : ICliFxBatchRunner
{
    private readonly CliFxService _service;

    public CliFxBatchRunner(LibAnalysisBridge bridge)
    {
        _service = new CliFxService(bridge);
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
            item.CliFramework,
            outputRoot,
            batchId,
            item.Attempt,
            source,
            timeouts.InstallTimeoutSeconds,
            timeouts.AnalysisTimeoutSeconds,
            timeouts.CommandTimeoutSeconds,
            cancellationToken);
}
