namespace InSpectra.Discovery.Tool.Analysis.Help;

internal sealed class HelpBatchRunner : IHelpBatchRunner
{
    private readonly HelpService _service = new();

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
