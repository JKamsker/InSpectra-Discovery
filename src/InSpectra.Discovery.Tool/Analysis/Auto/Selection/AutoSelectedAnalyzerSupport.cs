namespace InSpectra.Discovery.Tool.Analysis.Auto.Selection;

using InSpectra.Discovery.Tool.Analysis.Auto.Results;

using InSpectra.Discovery.Tool.Analysis.Tools;
using InSpectra.Discovery.Tool.Analysis.Output;
using InSpectra.Discovery.Tool.OpenCli.Documents;

using System.Text.Json.Nodes;

internal static class AutoSelectedAnalyzerSupport
{
    public static async Task<JsonObject> RunAsync(
        Func<CancellationToken, Task> runAnalyzerAsync,
        string packageId,
        string version,
        ToolDescriptor descriptor,
        string batchId,
        int attempt,
        string source,
        string resultPath,
        JsonObject? nativeResult,
        string selectedMode,
        string? selectedFramework,
        CancellationToken cancellationToken)
    {
        await runAnalyzerAsync(cancellationToken);

        var selectedResult = AutoResultSupport.LoadResult(resultPath)
            ?? AutoResultSupport.CreateFailureResult(
                packageId,
                version,
                batchId,
                attempt,
                source,
                "The selected analyzer did not write result.json.");
        AutoResultSupport.ApplyDescriptor(selectedResult, descriptor, selectedMode, nativeResult, selectedFramework);
        ValidateSuccessfulOpenCliArtifact(selectedResult, resultPath);
        return selectedResult;
    }

    private static void ValidateSuccessfulOpenCliArtifact(JsonObject selectedResult, string resultPath)
    {
        if (!string.Equals(selectedResult["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal))
        {
            return;
        }

        var openCliArtifact = selectedResult["artifacts"]?["opencliArtifact"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(openCliArtifact))
        {
            return;
        }

        var outputDirectory = Path.GetDirectoryName(resultPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        var openCliPath = Path.Combine(outputDirectory, openCliArtifact);
        if (OpenCliDocumentValidator.TryLoadValidDocument(openCliPath, out _, out var reason))
        {
            return;
        }

        var failureMessage = $"Selected analyzer produced an invalid OpenCLI artifact. {reason}";
        selectedResult["disposition"] = "retryable-failure";
        selectedResult["retryEligible"] = true;
        selectedResult["phase"] = "opencli-validation";
        selectedResult["classification"] = "invalid-success-artifact";
        selectedResult["failureMessage"] = failureMessage;
        selectedResult["failureSignature"] = ResultSupport.GetFailureSignature(
            "opencli-validation",
            "invalid-success-artifact",
            failureMessage);
        selectedResult["opencliSource"] = null;

        if (selectedResult["artifacts"] is JsonObject artifacts)
        {
            artifacts["opencliArtifact"] = null;
        }

        if (selectedResult["steps"]?["opencli"] is JsonObject openCliStep)
        {
            openCliStep["status"] = "invalid";
            openCliStep["classification"] = "invalid-success-artifact";
            openCliStep["message"] = reason;
            openCliStep.Remove("artifactSource");
        }
    }
}

