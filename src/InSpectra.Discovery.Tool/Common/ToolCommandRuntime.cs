using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal class ToolCommandRuntime
{
    private static readonly Regex AnsiCsiRegex = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static readonly Regex AnsiEscapeRegex = new(@"\x1B[@-_]", RegexOptions.Compiled);
    private static readonly TimeSpan ExitGracePeriod = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan OutputDrainGracePeriod = TimeSpan.FromSeconds(1);

    public SandboxEnvironment CreateSandboxEnvironment(string tempRoot)
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
            ["NO_COLOR"] = "1",
            ["TERM"] = "dumb",
            ["XDG_CONFIG_HOME"] = Path.Combine(tempRoot, "xdg-config"),
            ["XDG_CACHE_HOME"] = Path.Combine(tempRoot, "xdg-cache"),
            ["XDG_DATA_HOME"] = Path.Combine(tempRoot, "xdg-data"),
            ["XDG_RUNTIME_DIR"] = Path.Combine(tempRoot, "xdg-runtime"),
        };

        return new SandboxEnvironment(
            Values: values,
            Directories: values.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public virtual async Task<ProcessResult> InvokeProcessCaptureAsync(
        string filePath,
        IReadOnlyList<string> argumentList,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        string? sandboxRoot,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = filePath,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            },
        };

        foreach (var argument in argumentList)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        foreach (var pair in environment)
        {
            process.StartInfo.Environment[pair.Key] = pair.Value;
        }

        using var readerCancellation = new CancellationTokenSource();
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        process.Start();

        var stdoutTask = PumpStreamAsync(process.StandardOutput, stdout, readerCancellation.Token);
        var stderrTask = PumpStreamAsync(process.StandardError, stderr, readerCancellation.Token);
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var completedTask = await Task.WhenAny(waitTask, timeoutTask);
            var timedOut = completedTask == timeoutTask;
            if (timedOut)
            {
                TryKillProcess(process);
                await WaitForExitAsync(process, ExitGracePeriod);
            }
            else
            {
                await waitTask;
            }

            var drained = await TryWaitForCompletionAsync(stdoutTask, stderrTask, OutputDrainGracePeriod);
            if (!drained)
            {
                timedOut = true;
                TerminateSandboxProcesses(sandboxRoot);
                readerCancellation.Cancel();
                await TryWaitForCompletionAsync(stdoutTask, stderrTask, OutputDrainGracePeriod);
            }

            stopwatch.Stop();
            return new ProcessResult(
                Status: timedOut ? "timed-out" : process.ExitCode == 0 ? "ok" : "failed",
                TimedOut: timedOut,
                ExitCode: timedOut ? null : process.ExitCode,
                DurationMs: (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds),
                Stdout: stdout.ToString(),
                Stderr: stderr.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            TerminateSandboxProcesses(sandboxRoot);
            readerCancellation.Cancel();
            await TryWaitForCompletionAsync(stdoutTask, stderrTask, OutputDrainGracePeriod);
            throw;
        }
    }

    public void TerminateSandboxProcesses(string? sandboxRoot)
    {
        if (string.IsNullOrWhiteSpace(sandboxRoot))
        {
            return;
        }

        var sandboxPath = Path.GetFullPath(sandboxRoot);
        foreach (var candidate in Process.GetProcesses())
        {
            using (candidate)
            {
                if (candidate.Id == Environment.ProcessId)
                {
                    continue;
                }

                var executablePath = TryGetExecutablePath(candidate);
                if (executablePath is null
                    || !executablePath.StartsWith(sandboxPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TryKillProcess(candidate);
            }
        }
    }

    public string? ResolveInstalledCommandPath(string installDirectory, string commandName)
    {
        var candidates = new List<string>
        {
            Path.Combine(installDirectory, commandName),
            Path.Combine(installDirectory, commandName + ".exe"),
            Path.Combine(installDirectory, commandName + ".cmd"),
            Path.Combine(installDirectory, commandName + ".bat"),
        };

        return candidates.FirstOrDefault(File.Exists);
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

    private static async Task PumpStreamAsync(StreamReader reader, StringBuilder buffer, CancellationToken cancellationToken)
    {
        var chunk = new char[4096];
        while (true)
        {
            int readCount;
            try
            {
                readCount = await reader.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (readCount == 0)
            {
                return;
            }

            buffer.Append(chunk, 0, readCount);
        }
    }

    private static async Task<bool> TryWaitForCompletionAsync(Task stdoutTask, Task stderrTask, TimeSpan timeout)
    {
        var combinedTask = Task.WhenAll(stdoutTask, stderrTask);
        return await Task.WhenAny(combinedTask, Task.Delay(timeout)) == combinedTask;
    }

    private static async Task WaitForExitAsync(Process process, TimeSpan timeout)
    {
        if (process.HasExited)
        {
            return;
        }

        await Task.WhenAny(process.WaitForExitAsync(CancellationToken.None), Task.Delay(timeout));
    }

    private static string? TryGetExecutablePath(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return null;
            }

            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    public sealed record SandboxEnvironment(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlyList<string> Directories);

    public sealed record ProcessResult(
        string Status,
        bool TimedOut,
        int? ExitCode,
        int DurationMs,
        string Stdout,
        string Stderr)
    {
        public JsonObject ToJsonObject()
            => new()
            {
                ["status"] = Status,
                ["timedOut"] = TimedOut,
                ["exitCode"] = ExitCode,
                ["durationMs"] = DurationMs,
                ["stdout"] = NormalizeConsoleText(Stdout),
                ["stderr"] = NormalizeConsoleText(Stderr),
            };
    }
}
