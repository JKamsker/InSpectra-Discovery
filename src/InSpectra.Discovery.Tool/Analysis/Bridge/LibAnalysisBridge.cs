namespace InSpectra.Discovery.Tool.Analysis.Bridge;

using InSpectra.Discovery.Tool.Analysis.NonSpectre;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Lib.Contracts;
using InSpectra.Lib.Contracts.Providers;

using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class LibAnalysisBridge
{
    private readonly IPackageCliToolInstaller _installer;
    private readonly IAcquisitionAnalysisDispatcher _dispatcher;

    public LibAnalysisBridge(
        IPackageCliToolInstaller installer,
        IAcquisitionAnalysisDispatcher dispatcher)
    {
        _installer = installer;
        _dispatcher = dispatcher;
    }

    public async Task AnalyzeAsync(
        NonSpectreInstalledToolAnalysisRequest request,
        string mode,
        CancellationToken cancellationToken)
    {
        request.Result["phase"] = "install";

        var installStopwatch = Stopwatch.StartNew();
        PackageCliToolInstallation installation;
        try
        {
            installation = await _installer.InstallAsync(
                request.PackageId,
                request.Version,
                request.CommandName,
                request.CliFramework,
                request.TempRoot,
                request.InstallTimeoutSeconds,
                cancellationToken);
        }
        catch (Exception ex)
        {
            installStopwatch.Stop();
            request.Result["timings"]!.AsObject()["installMs"] = (int)Math.Round(installStopwatch.Elapsed.TotalMilliseconds);
            NonSpectreResultSupport.ApplyRetryableFailure(
                request.Result,
                phase: "install",
                classification: "install-failed",
                ex.Message);
            return;
        }

        installStopwatch.Stop();
        request.Result["timings"]!.AsObject()["installMs"] = (int)Math.Round(installStopwatch.Elapsed.TotalMilliseconds);
        request.Result["steps"]!.AsObject()["install"] = new JsonObject
        {
            ["status"] = "ok",
            ["durationMs"] = (int)Math.Round(installStopwatch.Elapsed.TotalMilliseconds),
        };

        if (installation.CliFramework is not null)
        {
            request.Result["cliFramework"] = installation.CliFramework;
        }

        var target = new CliTargetDescriptor(
            DisplayName: $"{request.PackageId} {request.Version}",
            CommandPath: installation.CommandPath,
            CommandName: installation.CommandName,
            WorkingDirectory: request.TempRoot,
            InstallDirectory: installation.InstallDirectory,
            PreferredEntryPointPath: installation.PreferredEntryPointPath,
            Version: request.Version,
            Environment: installation.Environment,
            CliFramework: installation.CliFramework,
            HookCliFramework: installation.HookCliFramework,
            PackageTitle: installation.PackageTitle,
            PackageDescription: installation.PackageDescription);

        request.Result["phase"] = "analysis";
        var analysisStopwatch = Stopwatch.StartNew();

        var outcome = await _dispatcher.TryAnalyzeAsync(
            target,
            mode,
            request.CliFramework ?? installation.CliFramework,
            request.CommandTimeoutSeconds,
            cancellationToken);

        analysisStopwatch.Stop();
        request.Result["timings"]!.AsObject()["crawlMs"] = (int)Math.Round(analysisStopwatch.Elapsed.TotalMilliseconds);

        if (outcome.Success && outcome.OpenCliJson is not null)
        {
            await File.WriteAllTextAsync(
                Path.Combine(request.OutputDirectory, "opencli.json"), outcome.OpenCliJson, cancellationToken);
            request.Result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";

            if (outcome.CrawlJson is not null)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(request.OutputDirectory, "crawl.json"), outcome.CrawlJson, cancellationToken);
                request.Result["artifacts"]!.AsObject()["crawlArtifact"] = "crawl.json";
            }

            NonSpectreResultSupport.ApplySuccess(
                request.Result,
                classification: outcome.FailureClassification ?? $"{mode}-crawl",
                artifactSource: $"lib-{mode}");
        }
        else
        {
            NonSpectreResultSupport.ApplyTerminalFailure(
                request.Result,
                phase: "analysis",
                classification: outcome.FailureClassification ?? $"{mode}-failed",
                outcome.FailureMessage);
        }

        if (outcome.Framework is not null)
        {
            request.Result["cliFramework"] = outcome.Framework;
        }

        request.Result["analysisMode"] = outcome.Mode;
    }
}
