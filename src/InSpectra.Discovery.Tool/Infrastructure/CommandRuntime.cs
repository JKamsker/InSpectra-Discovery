namespace InSpectra.Discovery.Tool.Infrastructure;

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

internal class CommandRuntime
{
    public SandboxEnvironment CreateSandboxEnvironment(string tempRoot)
        => CommandSandboxEnvironmentSupport.CreateSandboxEnvironment(tempRoot);

    public virtual Task<ProcessResult> InvokeProcessCaptureAsync(
        string filePath,
        IReadOnlyList<string> argumentList,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        string? sandboxRoot,
        CancellationToken cancellationToken)
        => CommandProcessSupport.InvokeProcessCaptureAsync(
            CreateProcess(filePath, workingDirectory, argumentList, environment),
            timeoutSeconds,
            sandboxRoot,
            cancellationToken,
            TerminateSandboxProcesses);

    public void TerminateSandboxProcesses(string? sandboxRoot)
        => CommandProcessSupport.TerminateSandboxProcesses(sandboxRoot);

    public string? ResolveInstalledCommandPath(string installDirectory, string commandName)
        => CommandProcessSupport.ResolveInstalledCommandPath(installDirectory, commandName);

    public static string? NormalizeConsoleText(string? value)
        => CommandProcessSupport.NormalizeConsoleText(value);

    private static Process CreateProcess(
        string filePath,
        string workingDirectory,
        IReadOnlyList<string> argumentList,
        IReadOnlyDictionary<string, string> environment)
    {
        var process = new Process
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

        return process;
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


