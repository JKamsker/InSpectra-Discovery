namespace InSpectra.Discovery.Tool.Analysis.Auto.Execution;

using InSpectra.Discovery.Tool.Analysis.Auto.Results;
using InSpectra.Discovery.Tool.Analysis.Auto.Runners;
using InSpectra.Discovery.Tool.Analysis.Auto.Selection;
using InSpectra.Discovery.Tool.Analysis.Tools;

using System.Text.Json.Nodes;

internal static class AutoAttemptSequenceSupport
{
    public static async Task<JsonObject> RunAsync(
        IReadOnlyList<AutoAnalysisAttempt> attempts,
        IAutoHelpRunner helpRunner,
        IAutoCliFxRunner cliFxRunner,
        IAutoStaticRunner staticRunner,
        IAutoHookRunner hookRunner,
        string packageId,
        string version,
        ToolDescriptor descriptor,
        string outputDirectory,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        string resultPath,
        JsonObject? nativeResult,
        CancellationToken cancellationToken)
    {
        JsonObject? bestFailureResult = null;
        AutoAnalysisAttempt? bestFailureAttempt = null;
        JsonObject? latestFailureResult = null;
        AutoAnalysisAttempt? latestFailureAttempt = null;
        var attemptLog = new JsonArray();

        foreach (var analysisAttempt in attempts)
        {
            var attemptResult = await AutoExecutionSupport.RunSelectedAnalyzerAsync(
                analysisAttempt,
                helpRunner,
                cliFxRunner,
                staticRunner,
                hookRunner,
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
                nativeResult,
                cancellationToken);
            AppendAttempt(attemptLog, analysisAttempt, attemptResult);

            if (AutoResultInspector.ShouldUseFallbackResult(attemptResult))
            {
                if (latestFailureResult is not null && latestFailureAttempt is not null)
                {
                    AutoResultSupport.ApplyFallback(attemptResult, latestFailureAttempt.Mode, latestFailureResult);
                }

                AutoResultSupport.ApplyAttemptLog(attemptResult, attemptLog);
                return attemptResult;
            }

            latestFailureResult = attemptResult;
            latestFailureAttempt = analysisAttempt;

            if (bestFailureResult is null)
            {
                bestFailureResult = attemptResult;
                bestFailureAttempt = analysisAttempt;
                continue;
            }

            if (AutoResultInspector.ShouldUseFailedFallbackResult(bestFailureResult, attemptResult))
            {
                AutoResultSupport.ApplyFallback(attemptResult, bestFailureAttempt!.Mode, bestFailureResult);
                bestFailureResult = attemptResult;
                bestFailureAttempt = analysisAttempt;
            }
        }

        var finalResult = bestFailureResult
            ?? AutoResultSupport.CreateFailureResult(packageId, version, batchId, attempt, source, "The auto analyzer did not execute any attempts.");
        AutoResultSupport.ApplyAttemptLog(finalResult, attemptLog);
        return finalResult;
    }

    private static void AppendAttempt(JsonArray attemptLog, AutoAnalysisAttempt attempt, JsonObject result)
    {
        attemptLog.Add(
            new JsonObject
            {
                ["mode"] = attempt.Mode,
                ["framework"] = attempt.Framework,
                ["disposition"] = result["disposition"]?.GetValue<string>(),
                ["classification"] = result["classification"]?.GetValue<string>(),
            });
    }
}
