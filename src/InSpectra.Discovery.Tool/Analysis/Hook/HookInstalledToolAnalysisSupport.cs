namespace InSpectra.Discovery.Tool.Analysis.Hook;

using InSpectra.Discovery.Tool.Frameworks;
using InSpectra.Discovery.Tool.Help.Inference.Text;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using InSpectra.Discovery.Tool.Analysis.NonSpectre;

using InSpectra.Discovery.Tool.Infrastructure.Commands;

using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;

internal sealed class HookInstalledToolAnalysisSupport
{
    internal const string ExpectedCliFrameworkEnvironmentVariableName = "INSPECTRA_EXPECTED_CLI_FRAMEWORK";
    internal const string GlobalizationInvariantEnvironmentVariableName = "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT";
    internal const string DotnetRollForwardEnvironmentVariableName = "DOTNET_ROLL_FORWARD";
    internal const string DotnetRollForwardMajorValue = "Major";
    private readonly CommandRuntime _runtime;
    private readonly Func<string?> _hookDllPathResolver;

    public HookInstalledToolAnalysisSupport()
        : this(new CommandRuntime(), ResolveHookDllPath)
    {
    }

    internal HookInstalledToolAnalysisSupport(CommandRuntime runtime, Func<string?> hookDllPathResolver)
    {
        _runtime = runtime;
        _hookDllPathResolver = hookDllPathResolver;
    }

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
        var installedTool = await CommandInstallationSupport.InstallToolAsync(
            _runtime,
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
        var hookDllPath = _hookDllPathResolver();
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
        var expectedHookCliFramework = CliFrameworkProviderRegistry.ResolveHookAnalysisFramework(
            result["cliFramework"]?.GetValue<string>());
        if (!string.IsNullOrWhiteSpace(expectedHookCliFramework))
        {
            hookEnvironment[ExpectedCliFrameworkEnvironmentVariableName] = expectedHookCliFramework;
        }

        // Execute the tool with the startup hook attached. The hook observes the command tree
        // while the target processes `--help`, then writes a capture file for OpenCLI generation.
        var hookStopwatch = Stopwatch.StartNew();
        var invocationResolution = HookToolProcessInvocationResolver.Resolve(
            installedTool.InstallDirectory,
            commandName,
            installedTool.CommandPath);
        if (invocationResolution.TerminalFailureClassification is not null)
        {
            NonSpectreResultSupport.ApplyTerminalFailure(
                result,
                phase: "hook-setup",
                classification: invocationResolution.TerminalFailureClassification,
                invocationResolution.TerminalFailureMessage);
            return;
        }

        var invocation = invocationResolution.Invocation!;
        var processResult = await InvokeWithHelpFallbackAsync(
            invocation,
            tempRoot,
            hookEnvironment,
            commandTimeoutSeconds,
            capturePath,
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
                HookFailureMessageSupport.BuildMissingCaptureMessage(processResult));
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

    private async Task<CommandRuntime.ProcessResult> InvokeWithHelpFallbackAsync(
        HookToolProcessInvocation invocation,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        string capturePath,
        CancellationToken cancellationToken)
    {
        var processResult = await InvokeWithInvariantGlobalizationRetryAsync(
            invocation,
            workingDirectory,
            environment,
            timeoutSeconds,
            capturePath,
            cancellationToken);
        if (!ShouldRetryWithAlternateHelp(processResult, capturePath))
        {
            return processResult;
        }

        foreach (var fallbackInvocation in HookToolProcessInvocationResolver.BuildHelpFallbackInvocations(invocation))
        {
            TryDeleteCaptureFile(capturePath);
            processResult = await InvokeWithInvariantGlobalizationRetryAsync(
                fallbackInvocation,
                workingDirectory,
                environment,
                timeoutSeconds,
                capturePath,
                cancellationToken);
            if (!ShouldRetryWithAlternateHelp(processResult, capturePath))
            {
                return processResult;
            }
        }

        return processResult;
    }

    private async Task<CommandRuntime.ProcessResult> InvokeWithInvariantGlobalizationRetryAsync(
        HookToolProcessInvocation invocation,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        string capturePath,
        CancellationToken cancellationToken)
    {
        var effectiveEnvironment = environment;
        var processResult = await InvokeHookProcessAsync(
            invocation,
            workingDirectory,
            effectiveEnvironment,
            timeoutSeconds,
            cancellationToken);
        if (!File.Exists(capturePath)
            && LooksLikeMissingIcu(processResult)
            && !effectiveEnvironment.ContainsKey(GlobalizationInvariantEnvironmentVariableName))
        {
            effectiveEnvironment = new Dictionary<string, string>(effectiveEnvironment, StringComparer.OrdinalIgnoreCase)
            {
                [GlobalizationInvariantEnvironmentVariableName] = "1",
            };

            TryDeleteCaptureFile(capturePath);
            processResult = await InvokeHookProcessAsync(
                invocation,
                workingDirectory,
                effectiveEnvironment,
                timeoutSeconds,
                cancellationToken);
            if (File.Exists(capturePath) || !LooksLikeRejectedHelpInvocation(processResult))
            {
                return processResult;
            }

            foreach (var fallbackInvocation in HookToolProcessInvocationResolver.BuildHelpFallbackInvocations(invocation))
            {
                TryDeleteCaptureFile(capturePath);
                processResult = await InvokeHookProcessAsync(
                    fallbackInvocation,
                    workingDirectory,
                    effectiveEnvironment,
                    timeoutSeconds,
                    cancellationToken);
                if (File.Exists(capturePath) || !LooksLikeRejectedHelpInvocation(processResult))
                {
                    return processResult;
                }
            }
        }

        if (File.Exists(capturePath)
            || !LooksLikeMissingSharedRuntime(processResult)
            || effectiveEnvironment.ContainsKey(DotnetRollForwardEnvironmentVariableName))
        {
            return processResult;
        }

        effectiveEnvironment = new Dictionary<string, string>(effectiveEnvironment, StringComparer.OrdinalIgnoreCase)
        {
            [DotnetRollForwardEnvironmentVariableName] = DotnetRollForwardMajorValue,
        };

        TryDeleteCaptureFile(capturePath);
        processResult = await InvokeHookProcessAsync(
            invocation,
            workingDirectory,
            effectiveEnvironment,
            timeoutSeconds,
            cancellationToken);
        if (File.Exists(capturePath) || !LooksLikeRejectedHelpInvocation(processResult))
        {
            return processResult;
        }

        foreach (var fallbackInvocation in HookToolProcessInvocationResolver.BuildHelpFallbackInvocations(invocation))
        {
            TryDeleteCaptureFile(capturePath);
            processResult = await InvokeHookProcessAsync(
                fallbackInvocation,
                workingDirectory,
                effectiveEnvironment,
                timeoutSeconds,
                cancellationToken);
            if (File.Exists(capturePath) || !LooksLikeRejectedHelpInvocation(processResult))
            {
                return processResult;
            }
        }

        return processResult;
    }

    private static bool ShouldRetryWithAlternateHelp(CommandRuntime.ProcessResult processResult, string capturePath)
    {
        if (!File.Exists(capturePath))
        {
            return LooksLikeRejectedHelpInvocation(processResult);
        }

        var capture = HookCaptureDeserializer.Deserialize(capturePath);
        if (capture is null || (capture.Status == "ok" && capture.Root is not null))
        {
            return false;
        }

        return LooksLikeRejectedHelpMessage(capture.Error);
    }

    private Task<CommandRuntime.ProcessResult> InvokeHookProcessAsync(
        HookToolProcessInvocation invocation,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        CancellationToken cancellationToken)
        => _runtime.InvokeProcessCaptureAsync(
            invocation.FilePath,
            invocation.ArgumentList,
            workingDirectory,
            environment,
            timeoutSeconds,
            workingDirectory,
            cancellationToken);

    private static bool LooksLikeRejectedHelpInvocation(CommandRuntime.ProcessResult processResult)
    {
        var lines = SplitLines(processResult.Stderr)
            .Concat(SplitLines(processResult.Stdout))
            .ToArray();
        return LooksLikeRejectedHelpLines(lines);
    }

    private static bool LooksLikeRejectedHelpMessage(string? message)
        => LooksLikeRejectedHelpLines(SplitLines(message).ToArray());

    private static bool LooksLikeRejectedHelpLines(IReadOnlyList<string?> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var firstLine = NormalizeRejectedHelpLine(lines[index]);
            var secondLine = index + 1 < lines.Count
                ? NormalizeRejectedHelpLine(lines[index + 1])
                : null;
            if (TextNoiseClassifier.LooksLikeRejectedHelpInvocation(firstLine, secondLine))
            {
                return true;
            }
        }

        return false;
    }

    private static string? NormalizeRejectedHelpLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var trimmed = line.Trim();
        var separatorIndex = trimmed.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex + 2 >= trimmed.Length)
        {
            return trimmed;
        }

        var prefix = trimmed[..separatorIndex];
        if (!prefix.EndsWith("Exception", StringComparison.OrdinalIgnoreCase)
            && !prefix.EndsWith("Error", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed[(separatorIndex + 2)..].TrimStart();
    }

    private static bool LooksLikeMissingIcu(CommandRuntime.ProcessResult processResult)
    {
        var combined = string.Join(
            "\n",
            SplitLines(processResult.Stderr)
                .Concat(SplitLines(processResult.Stdout))
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        if (string.IsNullOrWhiteSpace(combined))
        {
            return false;
        }

        return combined.Contains("Couldn't find a valid ICU package installed on the system", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("System.Globalization.Invariant", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("libicu", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMissingSharedRuntime(CommandRuntime.ProcessResult processResult)
    {
        var combined = string.Join(
            "\n",
            SplitLines(processResult.Stderr)
                .Concat(SplitLines(processResult.Stdout))
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        if (string.IsNullOrWhiteSpace(combined))
        {
            return false;
        }

        return combined.Contains("You must install or update .NET to run this application.", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("Framework: 'Microsoft.NETCore.App'", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("The following frameworks were found:", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string?> SplitLines(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static void TryDeleteCaptureFile(string capturePath)
    {
        try
        {
            if (File.Exists(capturePath))
            {
                File.Delete(capturePath);
            }
        }
        catch
        {
        }
    }
}
