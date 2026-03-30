namespace InSpectra.Discovery.Tool.Analysis;

using InSpectra.Discovery.Tool.Analysis.Introspection;
using System.Text.Json.Nodes;

internal sealed class InstalledToolAnalysisSupport
{
    public async Task AnalyzeAsync(
        JsonObject result,
        NuGetApiClient apiClient,
        string packageId,
        string version,
        string outputDirectory,
        string tempRoot,
        IReadOnlyDictionary<string, string> environment,
        string packageContentUrl,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var packageInspection = await new PackageArchiveInspector(apiClient).InspectAsync(packageContentUrl, cancellationToken);
        ResultSupport.MergePackageInspection(result["detection"]!.AsObject(), packageInspection);

        var commandName = packageInspection.ToolCommandNames.FirstOrDefault();
        result["command"] = commandName;
        result["entryPoint"] = packageInspection.ToolEntryPointPaths.FirstOrDefault();
        result["runner"] = null;
        result["toolSettingsPath"] = packageInspection.ToolSettingsPaths.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(commandName))
        {
            result["phase"] = "bootstrap";
            result["classification"] = "tool-command-missing";
            result["failureMessage"] = $"No tool command could be resolved for package '{packageId}' version '{version}'.";
            return;
        }

        var installDirectory = Path.Combine(tempRoot, "tool");
        var installResult = await RuntimeSupport.InvokeProcessCaptureAsync(
            "dotnet",
            ["tool", "install", packageId, "--version", version, "--tool-path", installDirectory],
            tempRoot,
            environment,
            installTimeoutSeconds,
            cancellationToken);
        result["steps"]!.AsObject()["install"] = installResult.ToStepMetadata(includeStdout: true);
        result["timings"]!.AsObject()["installMs"] = installResult.DurationMs;

        if (installResult.TimedOut || installResult.ExitCode != 0)
        {
            result["phase"] = "install";
            result["classification"] = installResult.TimedOut ? "install-timeout" : "install-failed";
            result["failureMessage"] = RuntimeSupport.GetPreferredMessage(installResult.Stdout, installResult.Stderr);
            return;
        }

        var commandPath = RuntimeSupport.ResolveInstalledCommandPath(installDirectory, commandName);
        if (commandPath is null)
        {
            result["phase"] = "install";
            result["classification"] = "installed-command-missing";
            result["failureMessage"] = $"Installed tool command '{commandName}' was not found.";
            return;
        }

        var openCliOutcome = await IntrospectionSupport.InvokeIntrospectionCommandAsync(
            commandPath,
            ["cli", "opencli"],
            "json",
            tempRoot,
            environment,
            commandTimeoutSeconds,
            cancellationToken);
        var xmlDocOutcome = await IntrospectionSupport.InvokeIntrospectionCommandAsync(
            commandPath,
            ["cli", "xmldoc"],
            "xml",
            tempRoot,
            environment,
            commandTimeoutSeconds,
            cancellationToken);

        IntrospectionSupport.ApplyOutputs(result, outputDirectory, openCliOutcome, xmlDocOutcome);
        IntrospectionSupport.ApplyClassification(result, openCliOutcome, xmlDocOutcome);
    }
}


