internal sealed class ToolHelpAnalysisService
{
    private static readonly NonSpectreAnalysisExecutionDefinition Definition = new(
        AnalysisMode: "help",
        TempRootPrefix: "inspectra-help",
        TimeoutLabel: "Help analysis");

    private readonly ToolCommandRuntime _runtime = new();
    private readonly ToolHelpInstalledToolAnalysisSupport _installedToolAnalyzer;

    public ToolHelpAnalysisService()
    {
        _installedToolAnalyzer = new ToolHelpInstalledToolAnalysisSupport(_runtime, new ToolHelpOpenCliBuilder());
    }

    public Task<int> RunQuietAsync(
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
        => NonSpectreAnalysisExecutionSupport.RunQuietAsync(
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
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        string? cliFramework,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        CancellationToken cancellationToken)
        => NonSpectreAnalysisExecutionSupport.RunAsync(
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
        System.Text.Json.Nodes.JsonObject result,
        string packageId,
        string version,
        string? commandName,
        CancellationToken cancellationToken)
    {
        using var scope = ToolRuntime.CreateNuGetApiClientScope();
        return await NonSpectreAnalysisBootstrapSupport.PopulateResultAsync(
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
