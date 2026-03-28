using System.Text.Json.Nodes;

internal interface IAutoAnalysisNativeRunner
{
    Task RunAsync(
        string packageId,
        string version,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);
}

internal interface IAutoAnalysisHelpRunner
{
    Task RunAsync(
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
        CancellationToken cancellationToken);
}

internal sealed class AutoAnalysisCommandService
{
    private readonly IToolAnalysisDescriptorResolver _descriptorResolver;
    private readonly IAutoAnalysisNativeRunner _nativeRunner;
    private readonly IAutoAnalysisHelpRunner _helpRunner;

    public AutoAnalysisCommandService()
        : this(
            new ToolAnalysisDescriptorResolver(),
            new AutoAnalysisNativeRunnerAdapter(),
            new AutoAnalysisHelpRunnerAdapter())
    {
    }

    internal AutoAnalysisCommandService(
        IToolAnalysisDescriptorResolver descriptorResolver,
        IAutoAnalysisNativeRunner nativeRunner,
        IAutoAnalysisHelpRunner helpRunner)
    {
        _descriptorResolver = descriptorResolver;
        _nativeRunner = nativeRunner;
        _helpRunner = helpRunner;
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
            var failure = CreateFailureResult(packageId, version, batchId, attempt, source, ex.Message);
            RepositoryPathResolver.WriteJsonFile(resultPath, failure);
            return await WriteResultAsync(packageId, version, resultPath, failure, json, suppressOutput, cancellationToken);
        }

        JsonObject? nativeResult = null;
        if (string.Equals(descriptor.PreferredAnalysisMode, "native", StringComparison.OrdinalIgnoreCase))
        {
            await _nativeRunner.RunAsync(
                packageId,
                version,
                outputDirectory,
                batchId,
                attempt,
                source,
                installTimeoutSeconds,
                commandTimeoutSeconds,
                cancellationToken);
            nativeResult = LoadResult(resultPath);
            if (nativeResult is not null)
            {
                ApplyDescriptor(nativeResult, descriptor, "native", null);
                RepositoryPathResolver.WriteJsonFile(resultPath, nativeResult);
                if (!AutoAnalysisResultInspector.ShouldTryHelpFallback(nativeResult))
                {
                    return await WriteResultAsync(packageId, version, resultPath, nativeResult, json, suppressOutput, cancellationToken);
                }
            }
        }

        await _helpRunner.RunAsync(
            packageId,
            version,
            descriptor.CommandName,
            outputDirectory,
            batchId,
            attempt,
            source,
            descriptor.CliFramework,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            cancellationToken);
        var helpResult = LoadResult(resultPath) ?? CreateFailureResult(packageId, version, batchId, attempt, source, "The selected analyzer did not write result.json.");
        ApplyDescriptor(helpResult, descriptor, "help", nativeResult);

        if (AutoAnalysisResultInspector.ShouldPreserveNativeResult(nativeResult, helpResult))
        {
            var preservedNativeResult = nativeResult!;
            RepositoryPathResolver.WriteJsonFile(resultPath, preservedNativeResult);
            return await WriteResultAsync(packageId, version, resultPath, preservedNativeResult, json, suppressOutput, cancellationToken);
        }

        RepositoryPathResolver.WriteJsonFile(resultPath, helpResult);
        return await WriteResultAsync(packageId, version, resultPath, helpResult, json, suppressOutput, cancellationToken);
    }

    private static JsonObject? LoadResult(string resultPath)
        => File.Exists(resultPath)
            ? JsonNode.Parse(File.ReadAllText(resultPath)) as JsonObject
            : null;

    private static void ApplyDescriptor(JsonObject result, ToolAnalysisDescriptor descriptor, string analysisMode, JsonObject? fallbackResult)
    {
        result["analysisMode"] = analysisMode;
        result["analysisSelection"] = new JsonObject
        {
            ["preferredMode"] = descriptor.PreferredAnalysisMode,
            ["selectedMode"] = analysisMode,
            ["reason"] = descriptor.SelectionReason,
        };

        if (result["cliFramework"] is null && !string.IsNullOrWhiteSpace(descriptor.CliFramework))
        {
            result["cliFramework"] = descriptor.CliFramework;
        }

        if (result["command"] is null && !string.IsNullOrWhiteSpace(descriptor.CommandName))
        {
            result["command"] = descriptor.CommandName;
        }

        if (result["packageUrl"] is null)
        {
            result["packageUrl"] = descriptor.PackageUrl;
        }

        if (result["packageContentUrl"] is null && !string.IsNullOrWhiteSpace(descriptor.PackageContentUrl))
        {
            result["packageContentUrl"] = descriptor.PackageContentUrl;
        }

        if (result["catalogEntryUrl"] is null && !string.IsNullOrWhiteSpace(descriptor.CatalogEntryUrl))
        {
            result["catalogEntryUrl"] = descriptor.CatalogEntryUrl;
        }

        if (fallbackResult is null)
        {
            return;
        }

        result["fallback"] = new JsonObject
        {
            ["from"] = "native",
            ["disposition"] = fallbackResult["disposition"]?.GetValue<string>(),
            ["classification"] = fallbackResult["classification"]?.GetValue<string>(),
            ["message"] = fallbackResult["failureMessage"]?.GetValue<string>(),
        };
    }

    private static JsonObject CreateFailureResult(string packageId, string version, string batchId, int attempt, string source, string message)
        => new()
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["batchId"] = batchId,
            ["attempt"] = attempt,
            ["source"] = source,
            ["analyzedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["disposition"] = "retryable-failure",
            ["phase"] = "selection",
            ["classification"] = "analysis-selection-failed",
            ["failureMessage"] = message,
            ["timings"] = new JsonObject { ["totalMs"] = null },
            ["steps"] = new JsonObject { ["install"] = null, ["opencli"] = null, ["xmldoc"] = null },
            ["artifacts"] = new JsonObject { ["opencliArtifact"] = null, ["xmldocArtifact"] = null },
        };

    private static async Task<int> WriteResultAsync(
        string packageId,
        string version,
        string resultPath,
        JsonObject result,
        bool json,
        bool suppressOutput,
        CancellationToken cancellationToken)
    {
        if (suppressOutput)
        {
            return 0;
        }

        var output = ToolRuntime.CreateOutput();
        return await output.WriteSuccessAsync(
            new
            {
                packageId,
                version,
                analysisMode = result["analysisMode"]?.GetValue<string>(),
                disposition = result["disposition"]?.GetValue<string>(),
                resultPath,
            },
            [
                new SummaryRow("Package", $"{packageId} {version}"),
                new SummaryRow("Mode", result["analysisMode"]?.GetValue<string>() ?? string.Empty),
                new SummaryRow("Disposition", result["disposition"]?.GetValue<string>() ?? string.Empty),
                new SummaryRow("Result artifact", resultPath),
            ],
            json,
            cancellationToken);
    }

    private sealed class AutoAnalysisNativeRunnerAdapter : IAutoAnalysisNativeRunner
    {
        private readonly AnalysisCommandService _service = new();

        public async Task RunAsync(string packageId, string version, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
            => await _service.RunQuietAsync(packageId, version, outputRoot, batchId, attempt, source, installTimeoutSeconds, commandTimeoutSeconds, cancellationToken);
    }

    private sealed class AutoAnalysisHelpRunnerAdapter : IAutoAnalysisHelpRunner
    {
        private readonly ToolHelpAnalysisService _service = new();

        public async Task RunAsync(string packageId, string version, string? commandName, string outputRoot, string batchId, int attempt, string source, string? cliFramework, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
            => await _service.RunQuietAsync(packageId, version, commandName, outputRoot, batchId, attempt, source, cliFramework, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, cancellationToken);
    }
}
