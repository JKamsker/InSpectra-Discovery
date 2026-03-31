namespace InSpectra.Discovery.Tool.Analysis.Help;

using InSpectra.Discovery.Tool.Analysis.Static;

internal sealed class StaticBatchRunner : IStaticBatchRunner
{
    private readonly StaticService _service = new();

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
