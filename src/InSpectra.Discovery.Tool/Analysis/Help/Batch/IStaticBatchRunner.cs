namespace InSpectra.Discovery.Tool.Analysis.Help.Batch;

using InSpectra.Discovery.Tool.Analysis.Help.Models;

internal interface IStaticBatchRunner
{
    Task<int> RunAsync(
        HelpBatchItem item,
        string outputRoot,
        string batchId,
        string source,
        HelpBatchTimeouts timeouts,
        CancellationToken cancellationToken);
}
