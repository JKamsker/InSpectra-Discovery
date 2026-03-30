namespace InSpectra.Discovery.Tool.Infrastructure;

using InSpectra.Discovery.Tool.Analysis;
using System.Text.Json.Nodes;

internal static class CommandInstallationSupport
{
    public static async Task<InstalledToolContext?> InstallToolAsync(
        CommandRuntime runtime,
        JsonObject result,
        string packageId,
        string version,
        string commandName,
        string tempRoot,
        int installTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var sandbox = runtime.CreateSandboxEnvironment(tempRoot);
        EnsureDirectories(sandbox.Directories);

        var installDirectory = Path.Combine(tempRoot, "tool");
        var installResult = await runtime.InvokeProcessCaptureAsync(
            "dotnet",
            ["tool", "install", packageId, "--version", version, "--tool-path", installDirectory],
            tempRoot,
            sandbox.Values,
            installTimeoutSeconds,
            tempRoot,
            cancellationToken);

        result["steps"]!.AsObject()["install"] = installResult.ToJsonObject();
        result["timings"]!.AsObject()["installMs"] = installResult.DurationMs;

        if (installResult.TimedOut || installResult.ExitCode != 0)
        {
            NonSpectreResultSupport.ApplyRetryableFailure(
                result,
                phase: "install",
                classification: installResult.TimedOut ? "install-timeout" : "install-failed",
                CommandRuntime.NormalizeConsoleText(installResult.Stdout)
                ?? CommandRuntime.NormalizeConsoleText(installResult.Stderr)
                ?? "Tool installation failed.");
            return null;
        }

        var commandPath = runtime.ResolveInstalledCommandPath(installDirectory, commandName);
        if (commandPath is null)
        {
            NonSpectreResultSupport.ApplyRetryableFailure(
                result,
                phase: "install",
                classification: "installed-command-missing",
                $"Installed tool command '{commandName}' was not found.");
            return null;
        }

        return new InstalledToolContext(
            Environment: sandbox.Values,
            InstallDirectory: installDirectory,
            CommandPath: commandPath);
    }

    public static void WriteCrawlArtifact(string outputDirectory, JsonObject result, JsonObject crawlArtifact)
    {
        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "crawl.json"), crawlArtifact);
        result["artifacts"]!.AsObject()["crawlArtifact"] = "crawl.json";
    }

    private static void EnsureDirectories(IReadOnlyList<string> directories)
    {
        foreach (var directory in directories)
        {
            Directory.CreateDirectory(directory);
        }
    }
}

internal sealed record InstalledToolContext(
    IReadOnlyDictionary<string, string> Environment,
    string InstallDirectory,
    string CommandPath);
