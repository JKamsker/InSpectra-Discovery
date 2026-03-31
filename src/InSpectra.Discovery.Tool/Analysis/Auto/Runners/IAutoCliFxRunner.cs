namespace InSpectra.Discovery.Tool.Analysis.Auto.Runners;

internal interface IAutoCliFxRunner
{
    Task RunAsync(
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
        CancellationToken cancellationToken);
}
