internal sealed class AutoAnalysisCommandService
{
    private readonly IToolAnalysisDescriptorResolver _descriptorResolver;
    private readonly IAutoAnalysisNativeRunner _nativeRunner;
    private readonly IAutoAnalysisHelpRunner _helpRunner;
    private readonly IAutoAnalysisCliFxRunner _cliFxRunner;
    private readonly IAutoAnalysisStaticRunner _staticRunner;

    public AutoAnalysisCommandService()
        : this(
            new ToolAnalysisDescriptorResolver(),
            new AutoAnalysisNativeRunnerAdapter(),
            new AutoAnalysisHelpRunnerAdapter(),
            new AutoAnalysisCliFxRunnerAdapter(),
            new AutoAnalysisStaticRunnerAdapter())
    {
    }

    internal AutoAnalysisCommandService(
        IToolAnalysisDescriptorResolver descriptorResolver,
        IAutoAnalysisNativeRunner nativeRunner,
        IAutoAnalysisHelpRunner helpRunner,
        IAutoAnalysisCliFxRunner cliFxRunner,
        IAutoAnalysisStaticRunner staticRunner)
    {
        _descriptorResolver = descriptorResolver;
        _nativeRunner = nativeRunner;
        _helpRunner = helpRunner;
        _cliFxRunner = cliFxRunner;
        _staticRunner = staticRunner;
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

        ToolAnalysisDescriptor descriptor;
        try
        {
            descriptor = await _descriptorResolver.ResolveAsync(packageId, version, cancellationToken);
        }
        catch (Exception ex)
        {
            var failure = AutoAnalysisResultSupport.CreateFailureResult(packageId, version, batchId, attempt, source, ex.Message);
            RepositoryPathResolver.WriteJsonFile(resultPath, failure);
            return await AutoAnalysisResultSupport.WriteResultAsync(packageId, version, resultPath, failure, json, suppressOutput, cancellationToken);
        }

        var nativeOutcome = await AutoAnalysisExecutionSupport.TryRunNativeAnalysisAsync(
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

        var selectedMode = AutoAnalysisModeSupport.ResolveFallbackMode(descriptor);
        var selectedResult = await AutoAnalysisExecutionSupport.RunSelectedAnalyzerAsync(
            selectedMode,
            _helpRunner,
            _cliFxRunner,
            _staticRunner,
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

        if (string.Equals(selectedMode, "help", StringComparison.Ordinal)
            && AutoAnalysisResultInspector.ShouldPreserveNativeResult(nativeOutcome.Result, selectedResult))
        {
            var preservedNativeResult = nativeOutcome.Result!;
            RepositoryPathResolver.WriteJsonFile(resultPath, preservedNativeResult);
            return await AutoAnalysisResultSupport.WriteResultAsync(packageId, version, resultPath, preservedNativeResult, json, suppressOutput, cancellationToken);
        }

        RepositoryPathResolver.WriteJsonFile(resultPath, selectedResult);
        return await AutoAnalysisResultSupport.WriteResultAsync(packageId, version, resultPath, selectedResult, json, suppressOutput, cancellationToken);
    }
}
