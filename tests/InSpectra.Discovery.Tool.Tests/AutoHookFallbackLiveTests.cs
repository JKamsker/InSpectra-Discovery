namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Auto.Services;

using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

[Collection("LiveToolAnalysis")]
public sealed class AutoHookFallbackLiveTests
{
    private const string EnableEnvVar = "INSPECTRA_DISCOVERY_LIVE_AUTO_TESTS";
    private readonly ITestOutputHelper _output;

    public AutoHookFallbackLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<HookFallbackToolCase> Cases()
    {
        var data = new TheoryData<HookFallbackToolCase>();
        data.Add(new HookFallbackToolCase(
            "System.CommandLine + Argu",
            "csharp-ls",
            "0.22.0",
            "csharp-ls",
            "csharp-ls",
            "0.22.0",
            "hook-no-assembly-loaded"));
        data.Add(new HookFallbackToolCase(
            "System.CommandLine",
            "DotNetAnalyzer",
            "1.5.0",
            "dotnet-analyzer",
            "DotNetAnalyzer - .NET MCP Server for Claude Code",
            "1.5.0",
            "hook-no-assembly-loaded"));
        data.Add(new HookFallbackToolCase(
            "System.CommandLine",
            "dotnet-mgcb",
            "3.8.5-preview.3",
            "mgcb",
            "MonoGame Content Builder:",
            "3.8.5-preview.3",
            "hook-no-assembly-loaded"));
        data.Add(new HookFallbackToolCase(
            "System.CommandLine",
            "dotnet-mgcb-editor-windows",
            "3.8.5-preview.3",
            "mgcb-editor-windows",
            "mgcb-editor-windows",
            "3.8.5-preview.3",
            "hook-no-assembly-loaded",
            expectedHookFailureClassifications:
            [
                "hook-no-assembly-loaded",
                "hook-target-unhandled-exception",
            ]));
        data.Add(new HookFallbackToolCase(
            "System.CommandLine",
            "SoftwareExtravaganza.Whizbang.CLI",
            "0.54.2-alpha.76",
            "whizbang",
            "Whizbang CLI - Command-line tool for Whizbang",
            "0.54.2-alpha.76",
            "hook-no-assembly-loaded"));
        data.Add(new HookFallbackToolCase(
            "McMaster.Extensions.CommandLineUtils",
            "sqlite-global-tool",
            "1.2.2",
            "sqlite-tool",
            "=> Welcome to sqlite .net core global tool version",
            "1.2.2",
            "hook-no-assembly-loaded",
            expectedAnalysisMode: "help",
            expectedClassification: "help-crawl",
            expectedArtifactSource: "crawled-from-help"));
        return data;
    }

    public static TheoryData<HookTerminalFailureToolCase> TerminalFailureCases()
    {
        var data = new TheoryData<HookTerminalFailureToolCase>();
        data.Add(new HookTerminalFailureToolCase(
            "Microsoft.Extensions.CommandLineUtils",
            "METU.CORE",
            "2025.805.1.1",
            "METU.CORE",
            "hook-invalid-dotnet-entrypoint"));
        data.Add(new HookTerminalFailureToolCase(
            "McMaster.Extensions.CommandLineUtils",
            "Meadow.Cli",
            "0.3.225",
            "meadow",
            "custom-parser-no-attributes",
            expectedAnalysisMode: "static",
            expectedPhase: "static-analysis",
            expectedSelectedMode: "static",
            expectedFallbackFrom: "hook",
            expectedFallbackClassification: "invalid-success-artifact"));
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Live")]
    public async Task RunAsync_Falls_Back_To_Static_For_Real_World_Hook_Regressions(HookFallbackToolCase testCase)
    {
        if (!ShouldRun())
        {
            return;
        }

        var service = new AutoCommandService();
        var outputRoot = Path.Combine(Path.GetTempPath(), "inspectra-live-auto-hook-fallback", Guid.NewGuid().ToString("N"));

        try
        {
            var exitCode = await service.RunAsync(
                testCase.PackageId,
                testCase.Version,
                outputRoot,
                batchId: "live-auto-hook-fallback",
                attempt: 1,
                source: "live-auto-hook-fallback-test",
                installTimeoutSeconds: 300,
                analysisTimeoutSeconds: 600,
                commandTimeoutSeconds: 60,
                json: false,
                cancellationToken: CancellationToken.None);

            Assert.Equal(0, exitCode);

            var resultPath = Path.Combine(outputRoot, "result.json");
            var openCliPath = Path.Combine(outputRoot, "opencli.json");
            Assert.True(File.Exists(resultPath), $"Missing result artifact for {testCase.PackageId}.");
            Assert.True(
                File.Exists(openCliPath),
                await LiveArtifactDiagnosticsSupport.BuildMissingArtifactMessageAsync(testCase.PackageId, outputRoot, resultPath, openCliPath));

            var result = JsonNode.Parse(await File.ReadAllTextAsync(resultPath));
            var openCli = JsonNode.Parse(await File.ReadAllTextAsync(openCliPath));

            Assert.Equal("success", result?["disposition"]?.GetValue<string>());
            Assert.Equal(testCase.ExpectedAnalysisMode, result?["analysisMode"]?.GetValue<string>());
            Assert.Equal(testCase.ExpectedClassification, result?["classification"]?.GetValue<string>());
            Assert.Equal(testCase.ExpectedArtifactSource, result?["opencliSource"]?.GetValue<string>());
            Assert.Equal(testCase.Framework, result?["cliFramework"]?.GetValue<string>());
            Assert.Equal("static", result?["analysisSelection"]?["preferredMode"]?.GetValue<string>());
            Assert.Equal(testCase.ExpectedAnalysisMode, result?["analysisSelection"]?["selectedMode"]?.GetValue<string>());
            Assert.Equal("hook", result?["fallback"]?["from"]?.GetValue<string>());
            Assert.Contains(
                result?["fallback"]?["classification"]?.GetValue<string>(),
                testCase.ExpectedHookFailureClassifications);
            Assert.Equal(testCase.CommandName, result?["command"]?.GetValue<string>());

            Assert.Equal(testCase.ExpectedOpenCliTitle, openCli?["info"]?["title"]?.GetValue<string>());
            Assert.Equal(testCase.ExpectedOpenCliVersion, openCli?["info"]?["version"]?.GetValue<string>());
            Assert.Equal(testCase.ExpectedArtifactSource, openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>());
            Assert.Equal(testCase.Framework, openCli?["x-inspectra"]?["cliFramework"]?.GetValue<string>());

            HookOpenCliSnapshotSupport.AssertMatchesFixture(testCase.PackageId, testCase.Version, openCli);
            _output.WriteLine($"{testCase.PackageId} {testCase.Version} succeeded via {testCase.ExpectedAnalysisMode} fallback after hook failure.");
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Theory]
    [MemberData(nameof(TerminalFailureCases))]
    [Trait("Category", "Live")]
    public async Task RunAsync_Reports_Honest_Terminal_Hook_Failures_For_Real_World_Tools(HookTerminalFailureToolCase testCase)
    {
        if (!ShouldRun())
        {
            return;
        }

        var service = new AutoCommandService();
        var outputRoot = Path.Combine(Path.GetTempPath(), "inspectra-live-auto-hook-terminal", Guid.NewGuid().ToString("N"));

        try
        {
            var exitCode = await service.RunAsync(
                testCase.PackageId,
                testCase.Version,
                outputRoot,
                batchId: "live-auto-hook-terminal",
                attempt: 1,
                source: "live-auto-hook-terminal-test",
                installTimeoutSeconds: 300,
                analysisTimeoutSeconds: 600,
                commandTimeoutSeconds: 60,
                json: false,
                cancellationToken: CancellationToken.None);

            Assert.Equal(0, exitCode);

            var resultPath = Path.Combine(outputRoot, "result.json");
            var openCliPath = Path.Combine(outputRoot, "opencli.json");
            Assert.True(File.Exists(resultPath), $"Missing result artifact for {testCase.PackageId}.");
            Assert.False(File.Exists(openCliPath), $"Unexpected OpenCLI artifact for {testCase.PackageId}: {openCliPath}");

            var result = JsonNode.Parse(await File.ReadAllTextAsync(resultPath));

            Assert.Equal("terminal-failure", result?["disposition"]?.GetValue<string>());
            Assert.False(result?["retryEligible"]?.GetValue<bool>() ?? true);
            Assert.Equal(testCase.ExpectedAnalysisMode, result?["analysisMode"]?.GetValue<string>());
            Assert.Equal(testCase.ExpectedPhase, result?["phase"]?.GetValue<string>());
            Assert.Equal(testCase.ExpectedClassification, result?["classification"]?.GetValue<string>());
            Assert.Equal(testCase.Framework, result?["cliFramework"]?.GetValue<string>());
            Assert.Equal(testCase.CommandName, result?["command"]?.GetValue<string>());
            Assert.Null(result?["opencliSource"]?.GetValue<string>());
            Assert.Equal("static", result?["analysisSelection"]?["preferredMode"]?.GetValue<string>());
            Assert.Equal(testCase.ExpectedSelectedMode, result?["analysisSelection"]?["selectedMode"]?.GetValue<string>());

            if (string.IsNullOrWhiteSpace(testCase.ExpectedFallbackFrom))
            {
                Assert.Null(result?["fallback"]);
            }
            else
            {
                Assert.Equal(testCase.ExpectedFallbackFrom, result?["fallback"]?["from"]?.GetValue<string>());
                Assert.Equal(testCase.ExpectedFallbackClassification, result?["fallback"]?["classification"]?.GetValue<string>());
            }

            HookResultSnapshotSupport.AssertMatchesFixture(testCase.PackageId, testCase.Version, result);
            _output.WriteLine($"{testCase.PackageId} {testCase.Version} correctly reports terminal hook failure {testCase.ExpectedClassification}.");
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static bool ShouldRun()
        => string.Equals(Environment.GetEnvironmentVariable(EnableEnvVar), "1", StringComparison.Ordinal);

    public sealed record HookFallbackToolCase(
        string Framework,
        string PackageId,
        string Version,
        string CommandName,
        string ExpectedOpenCliTitle,
        string ExpectedOpenCliVersion,
        string expectedHookFailureClassification,
        string expectedAnalysisMode = "static",
        string expectedClassification = "static-crawl",
        string expectedArtifactSource = "static-analysis",
        params string[] expectedHookFailureClassifications)
    {
        public IReadOnlyList<string> ExpectedHookFailureClassifications { get; } = expectedHookFailureClassifications.Length == 0
            ? [expectedHookFailureClassification]
            : expectedHookFailureClassifications;
        public string ExpectedAnalysisMode { get; } = expectedAnalysisMode;
        public string ExpectedClassification { get; } = expectedClassification;
        public string ExpectedArtifactSource { get; } = expectedArtifactSource;

        public override string ToString()
            => $"{PackageId} {Version}";
    }

    public sealed record HookTerminalFailureToolCase(
        string Framework,
        string PackageId,
        string Version,
        string CommandName,
        string ExpectedClassification,
        string expectedAnalysisMode = "hook",
        string expectedPhase = "hook-setup",
        string expectedSelectedMode = "hook",
        string? expectedFallbackFrom = null,
        string? expectedFallbackClassification = null)
    {
        public string ExpectedAnalysisMode { get; } = expectedAnalysisMode;
        public string ExpectedPhase { get; } = expectedPhase;
        public string ExpectedSelectedMode { get; } = expectedSelectedMode;
        public string? ExpectedFallbackFrom { get; } = expectedFallbackFrom;
        public string? ExpectedFallbackClassification { get; } = expectedFallbackClassification;

        public override string ToString()
            => $"{PackageId} {Version}";
    }
}
