internal interface IHelpBatchAnalysisRunner
{
    Task<int> RunAsync(
        HelpBatchItem item,
        string outputRoot,
        string batchId,
        string source,
        HelpBatchTimeouts timeouts,
        CancellationToken cancellationToken);
}

internal interface ICliFxBatchAnalysisRunner
{
    Task<int> RunAsync(
        HelpBatchItem item,
        string outputRoot,
        string batchId,
        string source,
        HelpBatchTimeouts timeouts,
        CancellationToken cancellationToken);
}

internal interface IStaticBatchAnalysisRunner
{
    Task<int> RunAsync(
        HelpBatchItem item,
        string outputRoot,
        string batchId,
        string source,
        HelpBatchTimeouts timeouts,
        CancellationToken cancellationToken);
}

internal sealed class ToolHelpBatchAnalysisRunner : IHelpBatchAnalysisRunner
{
    private readonly ToolHelpAnalysisService _service = new();

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

internal sealed class CliFxBatchAnalysisRunner : ICliFxBatchAnalysisRunner
{
    private readonly CliFxAnalysisService _service = new();

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

internal sealed class StaticBatchAnalysisRunner : IStaticBatchAnalysisRunner
{
    private readonly StaticAnalysisService _service = new();

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
