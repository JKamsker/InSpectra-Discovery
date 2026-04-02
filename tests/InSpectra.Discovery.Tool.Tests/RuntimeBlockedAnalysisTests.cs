namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.CliFx.Execution;
using InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;
using InSpectra.Discovery.Tool.Analysis.CliFx.OpenCli;
using InSpectra.Discovery.Tool.Analysis.NonSpectre;
using InSpectra.Discovery.Tool.Help.Crawling;
using InSpectra.Discovery.Tool.Help.OpenCli;
using InSpectra.Discovery.Tool.Infrastructure.Commands;

using System.Text.Json.Nodes;
using Xunit;

public sealed class RuntimeBlockedAnalysisTests
{
    [Fact]
    public async Task HelpAnalyzer_Reports_RuntimeBlocked_Failure_When_No_Help_Documents_Can_Be_Captured()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var runtime = new FakeInstallingRuntime("demo");
        var analyzer = new InstalledToolAnalyzer(runtime, new OpenCliBuilder());
        var result = CreateInitialResult("help");

        await analyzer.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("terminal-failure", result["disposition"]?.GetValue<string>());
        Assert.Equal("crawl", result["phase"]?.GetValue<string>());
        Assert.Equal("help-crawl-runtime-blocked", result["classification"]?.GetValue<string>());
        Assert.Contains("Microsoft.NETCore.App 9.0.0", result["failureMessage"]?.GetValue<string>());
        Assert.Contains("DOTNET_ROLL_FORWARD=Major", result["failureMessage"]?.GetValue<string>());
        Assert.Contains(
            runtime.AnalysisInvocations,
            invocation => invocation.Environment.ContainsKey(DotnetRuntimeCompatibilitySupport.DotnetRollForwardEnvironmentVariableName));
    }

    [Fact]
    public async Task CliFxAnalyzer_Reports_RuntimeBlocked_Failure_When_No_Help_Or_Metadata_Is_Available()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var runtime = new FakeInstallingRuntime("demo");
        var analyzer = new CliFxInstalledToolAnalysisSupport(
            runtime,
            new CliFxMetadataInspector(),
            new CliFxOpenCliBuilder(),
            new CliFxCoverageClassifier());
        var result = CreateInitialResult("clifx", cliFramework: "CliFx");

        await analyzer.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("terminal-failure", result["disposition"]?.GetValue<string>());
        Assert.Equal("crawl", result["phase"]?.GetValue<string>());
        Assert.Equal("clifx-runtime-blocked", result["classification"]?.GetValue<string>());
        Assert.Equal("missing-framework", result["coverage"]?["runtimeCompatibilityMode"]?.GetValue<string>());
        Assert.Contains("Microsoft.NETCore.App 9.0.0", result["failureMessage"]?.GetValue<string>());
        Assert.Contains(
            runtime.AnalysisInvocations,
            invocation => invocation.Environment.ContainsKey(DotnetRuntimeCompatibilitySupport.DotnetRollForwardEnvironmentVariableName));
    }

    [Fact]
    public async Task HelpAnalyzer_Reports_PlatformBlocked_Failure_When_Tool_Does_Not_Support_Current_Platform()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var runtime = new FakeInstallingRuntime("demo", UnsupportedPlatformResult);
        var analyzer = new InstalledToolAnalyzer(runtime, new OpenCliBuilder());
        var result = CreateInitialResult("help");

        await analyzer.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("terminal-failure", result["disposition"]?.GetValue<string>());
        Assert.Equal("crawl", result["phase"]?.GetValue<string>());
        Assert.Equal("help-crawl-platform-blocked", result["classification"]?.GetValue<string>());
        Assert.Contains("currently only supported on linux-x64", result["failureMessage"]?.GetValue<string>());
    }

    private static JsonObject CreateInitialResult(string analysisMode, string? cliFramework = null)
        => NonSpectreResultSupport.CreateInitialResult(
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            batchId: "batch-001",
            attempt: 1,
            source: "unit-test",
            cliFramework: cliFramework,
            analysisMode: analysisMode,
            analyzedAt: DateTimeOffset.Parse("2026-04-01T00:00:00Z"));

    private static CommandRuntime.ProcessResult MissingFrameworkResult()
        => new(
            Status: "failed",
            TimedOut: false,
            ExitCode: -2147450730,
            DurationMs: 1,
            Stdout: string.Empty,
            Stderr:
            """
            You must install or update .NET to run this application.

            Framework: 'Microsoft.NETCore.App', version '9.0.0' (x64)
            The following frameworks were found:
              10.0.5 at [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
            """);

    private static CommandRuntime.ProcessResult UnsupportedPlatformResult()
        => new(
            Status: "failed",
            TimedOut: false,
            ExitCode: 127,
            DurationMs: 1,
            Stdout:
            """
            AppMap for .NET is currently only supported on linux-x64.
            Platform win-x64 not supported yet.
            """,
            Stderr: string.Empty);

    private sealed class FakeInstallingRuntime(
        string commandName,
        Func<CommandRuntime.ProcessResult>? analysisResultFactory = null) : CommandRuntime
    {
        public List<InvocationRecord> AnalysisInvocations { get; } = [];

        public override Task<ProcessResult> InvokeProcessCaptureAsync(
            string filePath,
            IReadOnlyList<string> argumentList,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment,
            int timeoutSeconds,
            string? sandboxRoot,
            CancellationToken cancellationToken)
        {
            if (string.Equals(filePath, "dotnet", StringComparison.OrdinalIgnoreCase)
                && argumentList.Count >= 2
                && string.Equals(argumentList[0], "tool", StringComparison.OrdinalIgnoreCase)
                && string.Equals(argumentList[1], "install", StringComparison.OrdinalIgnoreCase))
            {
                var installDirectory = argumentList[^1];
                Directory.CreateDirectory(installDirectory);
                File.WriteAllText(Path.Combine(installDirectory, commandName + ".cmd"), "@echo off");
                return Task.FromResult(new CommandRuntime.ProcessResult(
                    Status: "ok",
                    TimedOut: false,
                    ExitCode: 0,
                    DurationMs: 1,
                    Stdout: string.Empty,
                    Stderr: string.Empty));
            }

            var invocation = new InvocationRecord(
                filePath,
                argumentList.ToArray(),
                new Dictionary<string, string>(environment, StringComparer.OrdinalIgnoreCase));
            AnalysisInvocations.Add(invocation);
            return Task.FromResult((analysisResultFactory ?? MissingFrameworkResult).Invoke());
        }
    }

    private sealed record InvocationRecord(
        string FilePath,
        string[] Arguments,
        IReadOnlyDictionary<string, string> Environment);
}
