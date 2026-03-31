namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

[Collection("LiveToolAnalysis")]
public sealed class HookServiceLiveTests
{
    private const string EnableEnvVar = "INSPECTRA_DISCOVERY_LIVE_HOOK_TESTS";
    private const string DotnetRootOverrideEnvVar = "INSPECTRA_DISCOVERY_LIVE_DOTNET_ROOT";
    private readonly ITestOutputHelper _output;

    public HookServiceLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<HookLiveToolCase> Cases()
    {
        var data = new TheoryData<HookLiveToolCase>();
        data.Add(new HookLiveToolCase(
            "AutoSDK.CLI",
            "0.30.1-dev.165",
            "autosdk",
            "AutoSDK.CLI",
            expectedCommands: ["generate", "http", "cli", "docs", "simplify", "convert-to-openapi30", "init", "trim", "ai"]));
        data.Add(new HookLiveToolCase(
            "AMSMigrate",
            "1.4.4",
            "amsmigrate",
            "Azure Media Services Asset Migration Tool",
            expectedCommands: ["analyze", "assets", "storage", "keys", "liveevents", "transforms"]));
        data.Add(new HookLiveToolCase(
            "CSharpier",
            "1.2.6",
            "csharpier",
            "CSharpier",
            expectedCommands: ["format", "check", "pipe-files", "server"]));
        data.Add(new HookLiveToolCase(
            "cassini.cli",
            "1.0.18",
            "cassini",
            "cassini",
            expectedCommands: ["login", "new", "update"],
            expectedOptions: ["version", "help"]));
        data.Add(new HookLiveToolCase(
            "CCPDF",
            "0.4.3",
            "ccpdf",
            "CCPDF",
            expectedCommands: ["compress", "resize", "rezip"]));
        data.Add(new HookLiveToolCase(
            "Duotify.MarkdownTranslator",
            "1.5.0",
            "mdt",
            "mdt",
            expectedArguments: ["file"]));
        data.Add(new HookLiveToolCase(
            "Slackjaw.Tools",
            "2026.3.30.64",
            "slackjaw",
            "Slackjaw.Tools",
            expectedCommands:
            [
                "build",
                "build-logic",
                "push",
                "build-push",
                "process-build-version",
                "check-updates",
                "list-unity",
            ]));
        data.Add(new HookLiveToolCase(
            "Walgelijk.FontGenerator",
            "1.6.0",
            "wfont",
            "Walgelijk.FontGenerator"));
        data.Add(new HookLiveToolCase(
            "PyWinRT",
            "3.2.1",
            "pywinrt",
            "PyWinRT"));
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Live")]
    public async Task RunAsync_Reproduces_Real_World_Hook_Regressions(HookLiveToolCase testCase)
    {
        if (!ShouldRun())
        {
            return;
        }

        var service = new HookService();
        var outputRoot = Path.Combine(Path.GetTempPath(), "inspectra-live-hook", Guid.NewGuid().ToString("N"));

        try
        {
            using var dotnetRootOverride = UseOptionalDotnetRootOverride();
            var exitCode = await service.RunAsync(
                testCase.PackageId,
                testCase.Version,
                testCase.CommandName,
                cliFramework: "System.CommandLine",
                outputRoot,
                batchId: "live-hook",
                attempt: 1,
                source: "live-hook-test",
                installTimeoutSeconds: 300,
                analysisTimeoutSeconds: 600,
                commandTimeoutSeconds: 60,
                json: false,
                cancellationToken: CancellationToken.None);

            Assert.Equal(0, exitCode);

            var resultPath = Path.Combine(outputRoot, "result.json");
            var openCliPath = Path.Combine(outputRoot, "opencli.json");
            Assert.True(File.Exists(resultPath), $"Missing result artifact for {testCase.PackageId}.");
            Assert.True(File.Exists(openCliPath), $"Missing OpenCLI artifact for {testCase.PackageId}.");

            var result = JsonNode.Parse(await File.ReadAllTextAsync(resultPath));
            var openCli = JsonNode.Parse(await File.ReadAllTextAsync(openCliPath));

            Assert.Equal("success", result?["disposition"]?.GetValue<string>());
            Assert.Equal("hook", result?["analysisMode"]?.GetValue<string>());
            Assert.Equal("startup-hook", result?["classification"]?.GetValue<string>());
            Assert.Equal("System.CommandLine", result?["cliFramework"]?.GetValue<string>());
            Assert.Equal(testCase.CommandName, result?["command"]?.GetValue<string>());

            Assert.Equal(testCase.ExpectedTitle, openCli?["info"]?["title"]?.GetValue<string>());
            Assert.Equal(testCase.Version, openCli?["info"]?["version"]?.GetValue<string>());
            Assert.Equal("startup-hook", openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>());
            Assert.Equal("System.CommandLine", openCli?["x-inspectra"]?["cliFramework"]?.GetValue<string>());
            var systemCommandLineVersion = openCli?["x-inspectra"]?["hookCapture"]?["systemCommandLineVersion"]?.GetValue<string>();
            Assert.NotNull(systemCommandLineVersion);
            Assert.StartsWith("2.0.", systemCommandLineVersion, StringComparison.Ordinal);

            var patchTarget = openCli?["x-inspectra"]?["hookCapture"]?["patchTarget"]?.GetValue<string>();
            Assert.NotNull(patchTarget);
            Assert.StartsWith("Parse-postfix", patchTarget, StringComparison.Ordinal);

            Assert.Equal("startup-hook", result?["steps"]?["opencli"]?["classification"]?.GetValue<string>());
            Assert.Equal("startup-hook", result?["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>());

            Assert.Equal(testCase.ExpectedCommands, GetTopLevelNames(openCli, "commands"));
            Assert.Equal(testCase.ExpectedOptions, GetTopLevelNames(openCli, "options"));
            Assert.Equal(testCase.ExpectedArguments, GetTopLevelNames(openCli, "arguments"));
            HookOpenCliSnapshotSupport.AssertMatchesFixture(testCase.PackageId, testCase.Version, openCli);

            _output.WriteLine($"{testCase.PackageId} {testCase.Version} succeeded via startup hook.");
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

    private static IDisposable UseOptionalDotnetRootOverride()
    {
        var overrideRoot = Environment.GetEnvironmentVariable(DotnetRootOverrideEnvVar);
        return string.IsNullOrWhiteSpace(overrideRoot)
            ? NoopDisposable.Instance
            : new DotnetRootOverrideScope(overrideRoot);
    }

    private static IReadOnlyList<string> GetTopLevelNames(JsonNode? document, string propertyName)
        => document?[propertyName]?.AsArray()
            .Select(item => item?["name"]?.GetValue<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray()
            ?? [];

    public sealed record HookLiveToolCase(
        string PackageId,
        string Version,
        string CommandName,
        string ExpectedTitle,
        IReadOnlyList<string>? expectedCommands = null,
        IReadOnlyList<string>? expectedOptions = null,
        IReadOnlyList<string>? expectedArguments = null)
    {
        public IReadOnlyList<string> ExpectedCommands { get; } = expectedCommands ?? [];
        public IReadOnlyList<string> ExpectedOptions { get; } = expectedOptions ?? [];
        public IReadOnlyList<string> ExpectedArguments { get; } = expectedArguments ?? [];

        public override string ToString()
            => $"{PackageId} {Version}";
    }

    private sealed class DotnetRootOverrideScope : IDisposable
    {
        private readonly string? _previousDotnetRoot;
        private readonly string? _previousDotnetRootX64;

        public DotnetRootOverrideScope(string dotnetRoot)
        {
            _previousDotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            _previousDotnetRootX64 = Environment.GetEnvironmentVariable("DOTNET_ROOT_X64");

            Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRoot);
            if (OperatingSystem.IsWindows())
            {
                Environment.SetEnvironmentVariable("DOTNET_ROOT_X64", dotnetRoot);
            }
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", _previousDotnetRoot);
            if (OperatingSystem.IsWindows())
            {
                Environment.SetEnvironmentVariable("DOTNET_ROOT_X64", _previousDotnetRootX64);
            }
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
