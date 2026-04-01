namespace InSpectra.Discovery.Tool.Analysis.Auto.Services;

using InSpectra.Discovery.Tool.Analysis.Auto.Selection;

using InSpectra.Discovery.Tool.Analysis.Auto.Execution;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.Analysis.Auto.Results;

using InSpectra.Discovery.Tool.Analysis.Auto.Runners;

using InSpectra.Discovery.Tool.Analysis.Tools;

using System.Text.Json.Nodes;

internal sealed class AutoCommandService
{
    private readonly IToolDescriptorResolver _descriptorResolver;
    private readonly IAutoNativeRunner _nativeRunner;
    private readonly IAutoHelpRunner _helpRunner;
    private readonly IAutoCliFxRunner _cliFxRunner;
    private readonly IAutoStaticRunner _staticRunner;
    private readonly IAutoHookRunner _hookRunner;

    public AutoCommandService()
        : this(
            new ToolDescriptorResolver(),
            new AutoNativeRunnerAdapter(),
            new AutoHelpRunnerAdapter(),
            new AutoCliFxRunnerAdapter(),
            new AutoStaticRunnerAdapter(),
            new AutoHookRunnerAdapter())
    {
    }

    internal AutoCommandService(
        IToolDescriptorResolver descriptorResolver,
        IAutoNativeRunner nativeRunner,
        IAutoHelpRunner helpRunner,
        IAutoCliFxRunner cliFxRunner,
        IAutoStaticRunner staticRunner,
        IAutoHookRunner hookRunner)
    {
        _descriptorResolver = descriptorResolver;
        _nativeRunner = nativeRunner;
        _helpRunner = helpRunner;
        _cliFxRunner = cliFxRunner;
        _staticRunner = staticRunner;
        _hookRunner = hookRunner;
    }

    public Task<int> RunAsync(
        string packageId,
        string version,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        CancellationToken cancellationToken)
        => RunCoreAsync(
            packageId,
            version,
            outputRoot,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            json,
            suppressOutput: false,
            cancellationToken);

    internal Task<int> RunQuietAsync(
        string packageId,
        string version,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
        => RunCoreAsync(
            packageId,
            version,
            outputRoot,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            json: false,
            suppressOutput: true,
            cancellationToken);

    private async Task<int> RunCoreAsync(
        string packageId,
        string version,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        bool suppressOutput,
        CancellationToken cancellationToken)
    {
        var outputDirectory = Path.GetFullPath(outputRoot);
        var resultPath = Path.Combine(outputDirectory, "result.json");
        Directory.CreateDirectory(outputDirectory);

        ToolDescriptor descriptor;
        try
        {
            descriptor = await _descriptorResolver.ResolveAsync(packageId, version, cancellationToken);
        }
        catch (Exception ex)
        {
            var failure = AutoResultSupport.CreateFailureResult(packageId, version, batchId, attempt, source, ex.Message);
            RepositoryPathResolver.WriteJsonFile(resultPath, failure);
            return await AutoResultSupport.WriteResultAsync(packageId, version, resultPath, failure, json, suppressOutput, cancellationToken);
        }

        var nativeOutcome = await AutoExecutionSupport.TryRunNativeAnalysisAsync(
            _nativeRunner,
            packageId,
            version,
            descriptor,
            outputDirectory,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            commandTimeoutSeconds,
            resultPath,
            json,
            suppressOutput,
            cancellationToken);

        if (nativeOutcome.ShouldReturnImmediately)
        {
            return nativeOutcome.ExitCode;
        }

        var selectedResult = await AutoAttemptSequenceSupport.RunAsync(
            AutoModeSupport.BuildAttemptPlan(descriptor),
            _helpRunner,
            _cliFxRunner,
            _staticRunner,
            _hookRunner,
            packageId,
            version,
            descriptor,
            outputDirectory,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            resultPath,
            nativeOutcome.Result,
            cancellationToken);
        var selectedMode = selectedResult["analysisMode"]?.GetValue<string>() ?? AutoModeSupport.ResolveFallbackMode(descriptor);

        if (string.Equals(selectedMode, "help", StringComparison.Ordinal)
            && AutoResultInspector.ShouldPreserveNativeResult(nativeOutcome.Result, selectedResult))
        {
            var preservedNativeResult = nativeOutcome.Result!;
            RepositoryPathResolver.WriteJsonFile(resultPath, preservedNativeResult);
            return await AutoResultSupport.WriteResultAsync(packageId, version, resultPath, preservedNativeResult, json, suppressOutput, cancellationToken);
        }

        RepositoryPathResolver.WriteJsonFile(resultPath, selectedResult);
        return await AutoResultSupport.WriteResultAsync(packageId, version, resultPath, selectedResult, json, suppressOutput, cancellationToken);
    }
}

