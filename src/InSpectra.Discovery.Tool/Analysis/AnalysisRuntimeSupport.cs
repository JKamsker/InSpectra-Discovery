using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class AnalysisRuntimeSupport
{
    private static readonly Regex AnsiCsiRegex = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static readonly Regex AnsiEscapeRegex = new(@"\x1B[@-_]", RegexOptions.Compiled);

    public static SandboxEnvironment CreateSandboxEnvironment(string tempRoot)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HOME"] = Path.Combine(tempRoot, "home"),
            ["DOTNET_CLI_HOME"] = Path.Combine(tempRoot, "dotnet-home"),
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION"] = "0",
            ["NUGET_PACKAGES"] = Path.Combine(tempRoot, "nuget-packages"),
            ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(tempRoot, "nuget-http-cache"),
            ["XDG_CONFIG_HOME"] = Path.Combine(tempRoot, "xdg-config"),
            ["XDG_CACHE_HOME"] = Path.Combine(tempRoot, "xdg-cache"),
            ["XDG_DATA_HOME"] = Path.Combine(tempRoot, "xdg-data"),
            ["XDG_RUNTIME_DIR"] = Path.Combine(tempRoot, "xdg-runtime"),
            ["TMPDIR"] = Path.Combine(tempRoot, "tmp"),
            ["CI"] = "true",
            ["NO_COLOR"] = "1",
            ["FORCE_COLOR"] = "0",
            ["TERM"] = "dumb",
            ["GCM_CREDENTIAL_STORE"] = "none",
            ["GCM_INTERACTIVE"] = "never",
            ["GIT_TERMINAL_PROMPT"] = "0",
        };

        values["TMP"] = values["TMPDIR"];
        values["TEMP"] = values["TMPDIR"];
        values["USERPROFILE"] = values["HOME"];
        values["APPDATA"] = values["XDG_CONFIG_HOME"];
        values["LOCALAPPDATA"] = values["XDG_DATA_HOME"];

        return new SandboxEnvironment(
            values,
            [
                values["HOME"],
                values["DOTNET_CLI_HOME"],
                values["NUGET_PACKAGES"],
                values["NUGET_HTTP_CACHE_PATH"],
                values["XDG_CONFIG_HOME"],
                values["XDG_CACHE_HOME"],
                values["XDG_DATA_HOME"],
                values["XDG_RUNTIME_DIR"],
                values["TMPDIR"],
            ]);
    }

    public static async Task<ProcessResult> InvokeProcessCaptureAsync(
        string filePath,
        IReadOnlyList<string> argumentList,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = filePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in argumentList)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var pair in environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        var stopwatch = Stopwatch.StartNew();
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);

        var completedTask = await Task.WhenAny(waitTask, timeoutTask);
        var timedOut = completedTask == timeoutTask;
        if (timedOut)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            await process.WaitForExitAsync(CancellationToken.None);
        }
        else
        {
            await waitTask;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        stopwatch.Stop();

        return new ProcessResult(
            Status: timedOut ? "timed-out" : process.ExitCode == 0 ? "ok" : "failed",
            TimedOut: timedOut,
            ExitCode: timedOut ? null : process.ExitCode,
            DurationMs: (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds),
            Stdout: stdout,
            Stderr: stderr);
    }

    public static string? ResolveInstalledCommandPath(string installDirectory, string commandName)
    {
        foreach (var candidate in new[]
        {
            Path.Combine(installDirectory, commandName),
            Path.Combine(installDirectory, commandName + ".exe"),
        })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? GetPreferredMessage(string? stdout, string? stderr)
    {
        var normalizedStderr = NormalizeConsoleText(stderr);
        if (!string.IsNullOrWhiteSpace(normalizedStderr))
        {
            return normalizedStderr;
        }

        var normalizedStdout = NormalizeConsoleText(stdout);
        return string.IsNullOrWhiteSpace(normalizedStdout) ? null : normalizedStdout;
    }

    public static string? NormalizeConsoleText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var normalized = value.TrimStart('\uFEFF').Replace("\0", string.Empty, StringComparison.Ordinal);
        normalized = AnsiCsiRegex.Replace(normalized, string.Empty);
        normalized = AnsiEscapeRegex.Replace(normalized, string.Empty);
        normalized = normalized.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

internal sealed record SandboxEnvironment(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<string> Directories);

internal sealed record ProcessResult(
    string Status,
    bool TimedOut,
    int? ExitCode,
    int DurationMs,
    string Stdout,
    string Stderr)
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
        };

        if (includeStdout)
        {
            metadata["stdout"] = AnalysisRuntimeSupport.NormalizeConsoleText(Stdout);
        }

        var normalizedStderr = AnalysisRuntimeSupport.NormalizeConsoleText(Stderr);
        if (!string.IsNullOrWhiteSpace(normalizedStderr))
        {
            metadata["stderr"] = normalizedStderr;
        }

        return metadata;
    }
}
