namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

[Collection("LiveToolAnalysis")]
public sealed class CommandLineUtilsHookLiveTests
{
    private static readonly string[] AllowedPatchTargetPrefixes =
    [
        "Parse-postfix",
        "Execute-postfix",
        "Execute-finalizer",
        "ProcessExit-fallback",
    ];

    private readonly ITestOutputHelper _output;

    public CommandLineUtilsHookLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<CommandLineUtilsHookToolCase> Cases()
    {
        var data = new TheoryData<CommandLineUtilsHookToolCase>();
        data.Add(new CommandLineUtilsHookToolCase(
            "McMaster.Extensions.CommandLineUtils",
            "dotnet-versioninfo",
            "1.0.3",
            "versioninfo",
            "versioninfo",
            expectedOptions: ["--relative", "--json", "--version", "--help"],
            expectedArguments: ["PATTERN"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "McMaster.Extensions.CommandLineUtils",
            "dotnet-tinify",
            "0.2.0",
            "dotnet-tinify",
            "dotnet tinify",
            expectedOptions: ["--help", "--api-key"],
            expectedArguments: ["PATH"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "McMaster.Extensions.CommandLineUtils",
            "Neo.Trace",
            "3.9.1",
            "neotrace",
            "neotrace",
            expectedCommands: ["block", "transaction"],
            expectedOptions: ["--version", "--help"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "McMaster.Extensions.CommandLineUtils",
            "Microsoft.Quantum.IQSharp",
            "0.28.302812",
            "dotnet-iqsharp",
            "dotnet-iqsharp",
            expectedCommands: ["server", "install", "kernel"],
            expectedOptions: ["--help", "--version", "--cacheFolder", "--workspace", "--skipAutoLoadProject"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "McMaster.Extensions.CommandLineUtils",
            "vbamc",
            "2.0.1",
            "vbamc",
            "vbamc",
            expectedOptions: ["--module", "--class", "--name", "--company", "--file", "--output", "--user-profile-path", "--property", "--help"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "McMaster.Extensions.CommandLineUtils",
            "Weixin.CLI",
            "0.1.1",
            "weixin",
            "weixin",
            expectedOptions: ["--help", "--Resource", "--QQGroup", "--keyword", "--platform", "--async", "--HostUrl"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "McMaster.Extensions.CommandLineUtils",
            "dotnet-tcloud",
            "1.0.1",
            "dotnet-tcloud",
            "dotnet-tcloud",
            expectedOptions: ["--help", "--cookie", "--uin", "--csrf"],
            expectedArguments: ["MARKDOWNFILEPATH"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "Microsoft.Extensions.CommandLineUtils",
            "SignClient",
            "1.3.155",
            "SignClient",
            "SignClient",
            expectedCommands: ["sign"],
            expectedOptions: ["--help", "--version"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "Microsoft.Extensions.CommandLineUtils",
            "NachoColl.Cloudformation4dotNET",
            "7.1.89",
            "dotnet-cf4dotnet",
            "cf4dotNet",
            expectedCommands: ["api", "lambdas"],
            expectedOptions: ["--help"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "Microsoft.Extensions.CommandLineUtils",
            "NuGetKeyVaultSignTool",
            "3.2.3",
            "NuGetKeyVaultSignTool",
            "NuGetKeyVaultSignTool",
            expectedCommands: ["sign", "verify"],
            expectedOptions: ["--help", "--version"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "Microsoft.Extensions.CommandLineUtils",
            "Tars.Net.CLI",
            "0.0.1-beta-20181021132156",
            "dotnet-tarsnet",
            "dotnet tarsnet",
            expectedCommands: ["codecs"],
            expectedOptions: ["--help", "--version"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "Microsoft.Extensions.CommandLineUtils",
            "CKSetup",
            "22.0.2",
            "cksetup",
            "CKSetup",
            expectedCommands: ["run", "store"],
            expectedOptions: ["--help", "--version"]));
        data.Add(new CommandLineUtilsHookToolCase(
            "Microsoft.Extensions.CommandLineUtils",
            "MetaCode.TemplateSuite2.CLI",
            "2.1.14",
            "mcts2",
            "mcts2",
            expectedCommands: ["g2c"]));
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Live")]
    public async Task RunAsync_Captures_Real_World_CommandLineUtils_Tools(CommandLineUtilsHookToolCase testCase)
    {
        if (!HookLiveTestSupport.ShouldRun())
        {
            return;
        }

        var service = new HookService(TestBridgeFactory.CreateBridge());
        var outputRoot = Path.Combine(Path.GetTempPath(), "inspectra-live-hook-commandlineutils", Guid.NewGuid().ToString("N"));

        try
        {
            using var dotnetRootOverride = HookLiveTestSupport.UseOptionalDotnetRootOverride();
            var exitCode = await service.RunAsync(
                testCase.PackageId,
                testCase.Version,
                testCase.CommandName,
                cliFramework: testCase.Framework,
                outputRoot,
                batchId: "live-hook-commandlineutils",
                attempt: 1,
                source: "live-hook-commandlineutils-test",
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
            Assert.Equal("hook", result?["analysisMode"]?.GetValue<string>());
            Assert.Equal("startup-hook", result?["classification"]?.GetValue<string>());
            Assert.Equal("startup-hook", result?["opencliSource"]?.GetValue<string>());
            Assert.Equal(testCase.Framework, result?["cliFramework"]?.GetValue<string>());
            Assert.Equal(testCase.CommandName, result?["command"]?.GetValue<string>());

            Assert.Equal(testCase.ExpectedTitle, openCli?["info"]?["title"]?.GetValue<string>());
            Assert.Equal(testCase.Version, openCli?["info"]?["version"]?.GetValue<string>());
            Assert.Equal("startup-hook", openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>());
            Assert.Equal(testCase.Framework, openCli?["x-inspectra"]?["cliFramework"]?.GetValue<string>());
            Assert.Equal(testCase.Framework, openCli?["x-inspectra"]?["hookCapture"]?["cliFramework"]?.GetValue<string>());
            Assert.NotNull(openCli?["x-inspectra"]?["hookCapture"]?["frameworkVersion"]?.GetValue<string>());
            HookLiveTestSupport.AssertPatchTarget(openCli, AllowedPatchTargetPrefixes);

            Assert.Equal("startup-hook", result?["steps"]?["opencli"]?["classification"]?.GetValue<string>());
            Assert.Equal("startup-hook", result?["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>());

            Assert.Equal(testCase.ExpectedCommands, HookLiveTestSupport.GetTopLevelNames(openCli, "commands"));
            Assert.Equal(testCase.ExpectedOptions, HookLiveTestSupport.GetTopLevelNames(openCli, "options"));
            Assert.Equal(testCase.ExpectedArguments, HookLiveTestSupport.GetTopLevelNames(openCli, "arguments"));
            HookOpenCliSnapshotSupport.AssertMatchesFixture(testCase.PackageId, testCase.Version, openCli);

            _output.WriteLine($"{testCase.PackageId} {testCase.Version} succeeded via startup hook for {testCase.Framework}.");
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    public sealed record CommandLineUtilsHookToolCase(
        string Framework,
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
