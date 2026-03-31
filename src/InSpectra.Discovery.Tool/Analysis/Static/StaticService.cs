namespace InSpectra.Discovery.Tool.Analysis.Static;

using InSpectra.Discovery.Tool.Infrastructure.Host;

using InSpectra.Discovery.Tool.StaticAnalysis.OpenCli;

using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

using InSpectra.Discovery.Tool.Analysis.NonSpectre;

internal sealed class StaticService
{
    private static readonly NonSpectreAnalysisExecutionDefinition Definition = new(
        AnalysisMode: "static",
        TempRootPrefix: "inspectra-static",
        TimeoutLabel: "Static analysis",
        DefaultCliFramework: "CommandLineParser",
        InitializeCoverage: true);

    private readonly StaticAnalysisRuntime _runtime = new();
    private readonly StaticInstalledToolAnalysisSupport _installedToolAnalyzer;

    public StaticService()
    {
        _installedToolAnalyzer = new StaticInstalledToolAnalysisSupport(
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


