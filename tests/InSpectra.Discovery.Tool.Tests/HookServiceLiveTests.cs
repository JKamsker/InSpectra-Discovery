namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

[Collection("LiveToolAnalysis")]
public sealed class HookServiceLiveTests
{
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
            expectedOptions: ["--help", "--version"],
            expectedCommands: ["generate", "http", "cli", "docs", "simplify", "convert-to-openapi30", "init", "trim", "ai"]));
        data.Add(new HookLiveToolCase(
            "AMSMigrate",
            "1.4.4",
            "amsmigrate",
            "Azure Media Services Asset Migration Tool",
            expectedCommands: ["analyze", "assets", "storage", "keys", "liveevents", "transforms"],
            expectedOptions: ["--log-level", "-l", "--subscription", "--resource-group", "--account-name", "--cloud-type", "-d", "--version", "-h"]));
        data.Add(new HookLiveToolCase(
            "CSharpier",
            "1.2.6",
            "csharpier",
            "CSharpier",
            expectedCommands: ["format", "check", "pipe-files", "server"],
            expectedOptions: ["--version", "-h"]));
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
            expectedCommands: ["compress", "resize", "rezip"],
            expectedOptions: ["--version", "-h"]));
        data.Add(new HookLiveToolCase(
            "Cnct",
            "0.5.0",
            "cnct",
            "Cnct",
            expectedOptions: ["config", "quiet", "debug", "version", "help"]));
        data.Add(new HookLiveToolCase(
            "Duotify.MarkdownTranslator",
            "1.5.0",
            "mdt",
            "mdt",
            expectedOptions: ["--in-place", "--output", "--force", "--progress", "--quite", "--lang", "--mode", "--version", "-h"],
            expectedArguments: ["FILE"]));
        data.Add(new HookLiveToolCase(
            "dotnet-httpie",
            "0.17.0-preview-20260321-070334",
            "dotnet-http",
            "dotnet-httpie",
            expectedCommands: ["exec", "test"],
            expectedOptions:
            [
                "--help",
                "--version",
                "--follow",
                "--max-redirects",
                "--no-verify",
                "--ssl",
                "--proxy",
                "--no-proxy",
                "--decompress",
                "-f",
                "-j",
                "--raw",
                "--debug",
                "--schema",
                "--httpVersion",
                "--auth-type",
                "--auth",
                "--no-cache",
                "-d",
                "-c",
                "-o",
                "--checksum",
                "--checksum-alg",
                "--json-schema-path",
                "--json-schema-out-format",
                "--offline",
                "--quiet",
                "-h",
                "-b",
                "-v",
                "-p",
                "--pretty",
                "--timeout",
                "-n",
                "--duration",
                "--vu",
                "--stream",
                "--exporter-type",
                "--export-json-path",
            ]));
        data.Add(new HookLiveToolCase(
            "retypeapp",
            "4.4.0",
            "retype",
            "retype",
            expectedCommands: ["start", "init", "build", "serve", "stop", "run", "watch", "clean", "wallet"],
            expectedOptions: ["--help", "--version", "--info"]));
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
            ],
            expectedOptions: ["--version", "-h"]));
        data.Add(new HookLiveToolCase(
            "Walgelijk.FontGenerator",
            "1.6.0",
            "wfont",
            "Walgelijk.FontGenerator",
            expectedOptions: ["--input", "--output", "--charset", "--font-size", "--version", "-h"]));
        data.Add(new HookLiveToolCase(
            "PyWinRT",
            "3.2.1",
            "pywinrt",
            "PyWinRT",
            expectedOptions: ["--input", "--reference", "--output", "--include", "--exclude", "--header-path", "--nullability-json", "--component-dlls", "--verbose", "--version", "-h"]));
        data.Add(new HookLiveToolCase(
            "PatliteTool",
            "1.4.0",
            "patlite",
            "PATLITE tool",
            expectedCommands: ["clear", "write", "read"],
            expectedOptions: ["--help", "--version"]));
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Live")]
    public async Task RunAsync_Reproduces_Real_World_Hook_Regressions(HookLiveToolCase testCase)
    {
        if (!HookLiveTestSupport.ShouldRun())
        {
            return;
        }

        var service = new HookService(TestBridgeFactory.CreateBridge());
        var outputRoot = Path.Combine(Path.GetTempPath(), "inspectra-live-hook", Guid.NewGuid().ToString("N"));

        try
        {
            using var dotnetRootOverride = HookLiveTestSupport.UseOptionalDotnetRootOverride();
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

            Assert.Equal(testCase.ExpectedCommands, HookLiveTestSupport.GetTopLevelNames(openCli, "commands"));
            Assert.Equal(testCase.ExpectedOptions, HookLiveTestSupport.GetTopLevelNames(openCli, "options"));
            Assert.Equal(testCase.ExpectedArguments, HookLiveTestSupport.GetTopLevelNames(openCli, "arguments"));
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
}
