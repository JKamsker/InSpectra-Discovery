namespace InSpectra.Discovery.Tool.Analysis.Hook;

using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;

internal sealed class HookInstalledToolAnalysisSupport
{
    public async Task AnalyzeAsync(
        JsonObject result,
        string packageId,
        string version,
        string commandName,
        string outputDirectory,
        string tempRoot,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var runtime = new CommandRuntime();

        var installedTool = await CommandInstallationSupport.InstallToolAsync(
            runtime,
            result,
            packageId,
            version,
            commandName,
            tempRoot,
            installTimeoutSeconds,
            cancellationToken);
        if (installedTool is null)
            return;

        // Resolve hook DLL path (deployed alongside the main tool assembly).
        var hookDllPath = ResolveHookDllPath();
        if (hookDllPath is null)
        {
            NonSpectreResultSupport.ApplyTerminalFailure(
                result,
                phase: "hook-setup",
                classification: "hook-dll-missing",
                "Could not locate InSpectra.Discovery.StartupHook.dll.");
            return;
        }

        // Prepare capture path and hook environment.
        var capturePath = Path.Combine(tempRoot, "inspectra-capture.json");
        var hookEnvironment = new Dictionary<string, string>(installedTool.Environment, StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_STARTUP_HOOKS"] = hookDllPath,
            ["INSPECTRA_CAPTURE_PATH"] = capturePath,
        };

        // Execute the tool — the hook will intercept System.CommandLine invocation,
        // write the capture file, and exit before the tool actually runs.
        var hookStopwatch = Stopwatch.StartNew();
        var processResult = await runtime.InvokeProcessCaptureAsync(
            installedTool.CommandPath,
            ["--help"],
            tempRoot,
            hookEnvironment,
            commandTimeoutSeconds,
            tempRoot,
            cancellationToken);
        hookStopwatch.Stop();

        result["timings"]!.AsObject()["crawlMs"] = (int)Math.Round(hookStopwatch.Elapsed.TotalMilliseconds);

        // Read and validate the capture file.
        if (!File.Exists(capturePath))
        {
            NonSpectreResultSupport.ApplyRetryableFailure(
                result,
                phase: "hook-capture",
                classification: "hook-no-capture-file",
                $"Startup hook did not produce a capture file. Exit code: {processResult.ExitCode}");
            return;
        }

        var capture = HookCaptureDeserializer.Deserialize(capturePath);
        if (capture is null)
        {
            NonSpectreResultSupport.ApplyRetryableFailure(
                result,
                phase: "hook-capture",
                classification: "hook-capture-invalid",
                "Capture file could not be deserialized.");
            return;
        }

        if (capture.Status != "ok" || capture.Root is null)
        {
            NonSpectreResultSupport.ApplyRetryableFailure(
                result,
                phase: "hook-capture",
                classification: $"hook-{capture.Status}",
                capture.Error ?? "Hook capture did not produce an ok result.");
            return;
        }

        // Build OpenCLI document from captured command tree.
        var openCliDocument = HookOpenCliBuilder.Build(commandName, version, capture);

        if (!string.IsNullOrWhiteSpace(result["cliFramework"]?.GetValue<string>()))
            openCliDocument["x-inspectra"]!.AsObject()["cliFramework"] = result["cliFramework"]!.GetValue<string>();

        OpenCliDocumentSanitizer.ApplyNuGetMetadata(
            openCliDocument,
            result["nugetTitle"]?.GetValue<string>(),
            result["nugetDescription"]?.GetValue<string>());

        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "opencli.json"), openCliDocument);
        result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";
        NonSpectreResultSupport.ApplySuccess(result, classification: "startup-hook", artifactSource: "startup-hook");
    }

    private static string? ResolveHookDllPath()
    {
        var toolAssemblyPath = Assembly.GetExecutingAssembly().Location;
        var toolDirectory = Path.GetDirectoryName(toolAssemblyPath);
        if (toolDirectory is null)
            return null;

        var hookPath = Path.Combine(toolDirectory, "hooks", "InSpectra.Discovery.StartupHook.dll");
        return File.Exists(hookPath) ? hookPath : null;
    }
}


