namespace InSpectra.Discovery.Tool.Analysis.Execution;


using System.Text;
using System.Text.Json.Nodes;

internal sealed record ProcessResult(
    string Status,
    bool TimedOut,
    int? ExitCode,
    int DurationMs,
    string Stdout,
    string Stderr,
    bool OutputLimitExceeded = false)
{
    public JsonObject ToStepMetadata(bool includeStdout)
    {
        var metadata = new JsonObject
        {
            ["status"] = Status,
            ["timedOut"] = TimedOut,
            ["exitCode"] = ExitCode,
            ["durationMs"] = DurationMs,
            ["stdoutLength"] = Encoding.UTF8.GetByteCount(Stdout ?? string.Empty),
            ["stderrLength"] = Encoding.UTF8.GetByteCount(Stderr ?? string.Empty),
            ["outputLimitExceeded"] = OutputLimitExceeded,
        };

        if (includeStdout)
        {
            metadata["stdout"] = RuntimeSupport.NormalizeConsoleText(Stdout);
        }

        var normalizedStderr = RuntimeSupport.NormalizeConsoleText(Stderr);
        if (!string.IsNullOrWhiteSpace(normalizedStderr))
        {
            metadata["stderr"] = normalizedStderr;
        }

        return metadata;
    }
}
