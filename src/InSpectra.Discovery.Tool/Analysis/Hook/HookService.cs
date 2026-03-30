namespace InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json.Nodes;

internal sealed class HookService
{
    private static readonly NonSpectreAnalysisExecutionDefinition Definition = new(
        AnalysisMode: "hook",
        TempRootPrefix: "inspectra-hook",
        TimeoutLabel: "startup hook analysis",
        DefaultCliFramework: "System.CommandLine",
        InitializeCoverage: false);

    private readonly CommandRuntime _runtime = new();
    private readonly HookInstalledToolAnalysisSupport _installedToolAnalyzer = new();

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
            AnalyzeInstalledToolAsync,
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
            AnalyzeInstalledToolAsync,
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
        JsonObject result,
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

    private Task AnalyzeInstalledToolAsync(NonSpectreInstalledToolAnalysisRequest request, CancellationToken cancellationToken)
        => _installedToolAnalyzer.AnalyzeAsync(
            request.Result,
            request.PackageId,
            request.Version,
            request.CommandName,
            request.OutputDirectory,
            request.TempRoot,
            request.InstallTimeoutSeconds,
            request.CommandTimeoutSeconds,
            cancellationToken);
}


