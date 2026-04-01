namespace InSpectra.Discovery.Tool.Infrastructure.Commands;

using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Infrastructure.Artifacts;

using InSpectra.Discovery.Tool.Analysis.NonSpectre;


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

    public static bool TryWriteCrawlArtifactOrApplyFailure(string outputDirectory, JsonObject result, JsonObject crawlArtifact)
    {
        if (TryWriteCrawlArtifact(outputDirectory, result, crawlArtifact, out var validationError))
        {
            return true;
        }

        NonSpectreResultSupport.ApplyTerminalFailure(
            result,
            phase: "crawl",
            classification: "crawl-artifact-too-large",
            validationError ?? "Generated crawl.json exceeded the allowed size.");
        return false;
    }

    public static bool TryWriteCrawlArtifact(string outputDirectory, JsonObject result, JsonObject crawlArtifact, out string? validationError)
    {
        var crawlPath = Path.Combine(outputDirectory, "crawl.json");
        var artifacts = result["artifacts"]?.AsObject()
            ?? throw new InvalidOperationException("Result is missing artifacts container.");

        if (!CrawlArtifactValidationSupport.TryValidate(crawlArtifact, out _, out validationError))
        {
            artifacts.Remove("crawlArtifact");
            if (File.Exists(crawlPath))
            {
                File.Delete(crawlPath);
            }

            return false;
        }

        RepositoryPathResolver.WriteJsonFile(crawlPath, crawlArtifact);
        artifacts["crawlArtifact"] = "crawl.json";
        return true;
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

