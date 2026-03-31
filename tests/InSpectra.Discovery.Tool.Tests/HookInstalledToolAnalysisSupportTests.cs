namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Hook;
using InSpectra.Discovery.Tool.Analysis.NonSpectre;
using InSpectra.Discovery.Tool.Infrastructure.Commands;

using System.Text.Json;
using System.Text.Json.Nodes;

using Xunit;

public sealed class HookInstalledToolAnalysisSupportTests
{
    [Fact]
    public async Task AnalyzeAsync_Applies_Missing_Capture_Failure_With_Process_Diagnostics()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var capturePath = Path.Combine(tempDirectory.Path, "inspectra-capture.json");
        var runtime = new FakeHookCommandRuntime("demo", invocation =>
        {
            Assert.Equal(Path.Combine(tempDirectory.Path, "tool", "demo.cmd"), invocation.FilePath);
            Assert.Single(invocation.ArgumentList);
            Assert.Equal("--help", invocation.ArgumentList[0]);
            Assert.Equal(hookDllPath, invocation.Environment["DOTNET_STARTUP_HOOKS"]);
            Assert.Equal(capturePath, invocation.Environment["INSPECTRA_CAPTURE_PATH"]);

            return new CommandRuntime.ProcessResult(
                Status: "failed",
                TimedOut: false,
                ExitCode: -532462766,
                DurationMs: 27,
                Stdout: string.Empty,
                Stderr: "Unhandled exception. System.NullReferenceException: boom");
        });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult();

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("retryable-failure", result["disposition"]?.GetValue<string>());
        Assert.Equal("hook-capture", result["phase"]?.GetValue<string>());
        Assert.Equal("hook-no-capture-file", result["classification"]?.GetValue<string>());

        var failureMessage = result["failureMessage"]?.GetValue<string>();
        Assert.NotNull(failureMessage);
        Assert.Contains("Exit code: -532462766.", failureMessage);
        Assert.Contains("stderr: Unhandled exception. System.NullReferenceException: boom", failureMessage);
    }

    [Fact]
    public async Task AnalyzeAsync_Writes_OpenCli_Artifact_When_Hook_Capture_Succeeds()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var runtime = new FakeHookCommandRuntime("demo", invocation =>
        {
            var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
            File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
            {
                CaptureVersion = 1,
                Status = "ok",
                SystemCommandLineVersion = "2.0.0",
                PatchTarget = "Parse-postfix",
                Root = new HookCapturedCommand
                {
                    Name = "demo",
                    Description = "Demo CLI",
                    Options =
                    [
                        new HookCapturedOption
                        {
                            Name = "--verbose",
                            Description = "Verbose output.",
                            ValueType = "Boolean",
                            Aliases = ["-v"],
                        },
                    ],
                    Subcommands =
                    [
                        new HookCapturedCommand
                        {
                            Name = "serve",
                            Description = "Start the server.",
                        },
                    ],
                },
            }));

            return new CommandRuntime.ProcessResult(
                Status: "ok",
                TimedOut: false,
                ExitCode: 0,
                DurationMs: 15,
                Stdout: string.Empty,
                Stderr: string.Empty);
        });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult();

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("complete", result["phase"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
        Assert.Equal("opencli.json", result["artifacts"]?["opencliArtifact"]?.GetValue<string>());

        var openCliPath = Path.Combine(tempDirectory.Path, "opencli.json");
        Assert.True(File.Exists(openCliPath));

        var openCli = JsonNode.Parse(File.ReadAllText(openCliPath))!.AsObject();
        Assert.Equal("demo", openCli["info"]?["title"]?.GetValue<string>());
        Assert.Equal("1.2.3", openCli["info"]?["version"]?.GetValue<string>());
        Assert.Equal("startup-hook", openCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
        Assert.Contains(openCli["options"]!.AsArray(), option => option?["name"]?.GetValue<string>() == "--verbose");
        Assert.Contains(openCli["commands"]!.AsArray(), command => command?["name"]?.GetValue<string>() == "serve");
    }

    private static JsonObject CreateInitialResult()
        => NonSpectreResultSupport.CreateInitialResult(
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            batchId: "batch-001",
            attempt: 1,
            source: "unit-test",
            cliFramework: "System.CommandLine",
            analysisMode: "hook",
            analyzedAt: DateTimeOffset.Parse("2026-03-31T00:00:00Z"));

    private static string CreateHookPlaceholder(string tempRoot)
    {
        var hookPath = Path.Combine(tempRoot, "hooks", "InSpectra.Discovery.StartupHook.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(hookPath)!);
        File.WriteAllText(hookPath, string.Empty);
        return hookPath;
    }

    private sealed class FakeHookCommandRuntime(
        string commandName,
        Func<HookInvocation, CommandRuntime.ProcessResult> hookHandler) : CommandRuntime
    {
        public override Task<ProcessResult> InvokeProcessCaptureAsync(
            string filePath,
            IReadOnlyList<string> argumentList,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment,
            int timeoutSeconds,
            string? sandboxRoot,
            CancellationToken cancellationToken)
        {
            if (string.Equals(filePath, "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                var installDirectory = argumentList[^1];
                Directory.CreateDirectory(installDirectory);
                File.WriteAllText(Path.Combine(installDirectory, commandName + ".cmd"), "@echo off");

                return Task.FromResult(new CommandRuntime.ProcessResult(
                    Status: "ok",
                    TimedOut: false,
                    ExitCode: 0,
                    DurationMs: 12,
                    Stdout: string.Empty,
                    Stderr: string.Empty));
            }

            var invocation = new HookInvocation(
                filePath,
                argumentList.ToArray(),
                new Dictionary<string, string>(environment, StringComparer.OrdinalIgnoreCase));
            return Task.FromResult(hookHandler(invocation));
        }
    }

    private sealed record HookInvocation(
        string FilePath,
        string[] ArgumentList,
        IReadOnlyDictionary<string, string> Environment);
}
