internal sealed class StaticAnalysisService
{
    private static readonly NonSpectreAnalysisExecutionDefinition Definition = new(
        AnalysisMode: "static",
        TempRootPrefix: "inspectra-static",
        TimeoutLabel: "Static analysis",
        DefaultCliFramework: "CommandLineParser",
        InitializeCoverage: true);

    private readonly StaticAnalysisToolRuntime _runtime = new();
    private readonly StaticAnalysisInstalledToolAnalysisSupport _installedToolAnalyzer;

    public StaticAnalysisService()
    {
        _installedToolAnalyzer = new StaticAnalysisInstalledToolAnalysisSupport(
            _runtime,
            new StaticAnalysisAssemblyInspectionSupport(new DnlibAssemblyScanner()),
            new StaticAnalysisOpenCliBuilder(),
            new StaticAnalysisCoverageClassifier());
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
            request.CliFramework ?? Definition.DefaultCliFramework ?? string.Empty,
            request.OutputDirectory,
            request.TempRoot,
            request.InstallTimeoutSeconds,
            request.CommandTimeoutSeconds,
            cancellationToken);
}
