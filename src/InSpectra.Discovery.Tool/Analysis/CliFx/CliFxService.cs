namespace InSpectra.Discovery.Tool.Analysis.CliFx;

using InSpectra.Discovery.Tool.Analysis.Bridge;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Analysis.NonSpectre;
using InSpectra.Discovery.Tool.Infrastructure.Commands;
using InSpectra.Lib.Contracts;

internal sealed class CliFxService
{
    private static readonly NonSpectreAnalysisExecutionDefinition Definition = new(
        AnalysisMode: AnalysisMode.CliFx,
        TempRootPrefix: "inspectra-clifx",
        TimeoutLabel: "CliFx analysis",
        DefaultCliFramework: "CliFx",
        InitializeCoverage: true);

    private readonly CommandRuntime _runtime = new();
    private readonly LibAnalysisBridge _bridge;

    public CliFxService(LibAnalysisBridge bridge)
    {
        _bridge = bridge;
    }

    public Task<int> RunQuietAsync(
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
        CancellationToken cancellationToken)
        => NonSpectreExecutionSupport.RunQuietAsync(
            _runtime,
            Definition,
            BootstrapAsync,
            (request, ct) => _bridge.AnalyzeAsync(request, AnalysisMode.CliFx, ct),
            packageId,
            version,
            commandName,
            cliFramework,
            outputRoot,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            cancellationToken);

    public Task<int> RunAsync(
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
        bool json,
        CancellationToken cancellationToken)
        => NonSpectreExecutionSupport.RunAsync(
            _runtime,
            Definition,
            BootstrapAsync,
            (request, ct) => _bridge.AnalyzeAsync(request, AnalysisMode.CliFx, ct),
            packageId,
            version,
            commandName,
            cliFramework,
            outputRoot,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            json,
            cancellationToken);

    private static async Task<NonSpectreAnalysisBootstrapResult> BootstrapAsync(
        System.Text.Json.Nodes.JsonObject result,
        string packageId,
        string version,
        string? commandName,
        CancellationToken cancellationToken)
    {
        using var scope = Runtime.CreateNuGetApiClientScope();
        return await NonSpectreBootstrapSupport.PopulateResultAsync(
            result,
            scope.Client,
            packageId,
            version,
            commandName,
            cancellationToken);
    }
}
