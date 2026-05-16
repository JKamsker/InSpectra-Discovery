namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Auto.Services;

using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

[Collection("LiveToolAnalysis")]
public sealed class AutoAnalysisServiceLiveTests
{
    private const string EnableEnvVar = "INSPECTRA_DISCOVERY_LIVE_AUTO_TESTS";
    private readonly ITestOutputHelper _output;

    public AutoAnalysisServiceLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<LiveAutoToolCase> Cases()
        => ValidatedGenericHelpFrameworkCases.LoadForAutoLiveTests();

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Live")]
    public async Task RunAsync_Chooses_Expected_Mode_For_Real_World_Tools(LiveAutoToolCase testCase)
    {
        if (!ShouldRun())
        {
            return;
        }

        var service = new AutoCommandService(TestBridgeFactory.CreateBridge());
        var outputRoot = Path.Combine(Path.GetTempPath(), "inspectra-live-auto", Guid.NewGuid().ToString("N"));

        try
        {
            var exitCode = await service.RunAsync(
                testCase.PackageId,
                testCase.Version,
                outputRoot,
                batchId: $"live-auto-{testCase.Framework}",
                attempt: 1,
                source: "live-auto-test",
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
            var document = JsonNode.Parse(await File.ReadAllTextAsync(openCliPath));
            Assert.Equal("success", result?["disposition"]?.GetValue<string>());
            Assert.Equal(testCase.ExpectedAnalysisMode, result?["analysisMode"]?.GetValue<string>());
            Assert.Equal(testCase.Framework, result?["cliFramework"]?.GetValue<string>());

            foreach (var expectedCommand in testCase.ExpectedCommands)
            {
                Assert.True(ContainsCommand(document, expectedCommand), $"Expected command '{expectedCommand}' in {testCase.PackageId}.");
            }

            foreach (var expectedOption in testCase.ExpectedOptions)
            {
                Assert.True(ContainsOption(document, expectedOption), $"Expected option '{expectedOption}' in {testCase.PackageId}.");
            }

            foreach (var expectedArgument in testCase.ExpectedArguments)
            {
                Assert.True(ContainsArgument(document, expectedArgument), $"Expected argument '{expectedArgument}' in {testCase.PackageId}.");
            }

            _output.WriteLine($"{testCase.PackageId} {testCase.Version} succeeded in {testCase.ExpectedAnalysisMode} mode.");
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

    private static bool ContainsCommand(JsonNode? node, string expectedName)
    {
        foreach (var command in node?["commands"]?.AsArray() ?? [])
        {
            if (string.Equals(command?["name"]?.GetValue<string>(), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ContainsCommand(command, expectedName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsOption(JsonNode? node, string expectedName)
    {
        foreach (var option in node?["options"]?.AsArray() ?? [])
        {
            if (string.Equals(option?["name"]?.GetValue<string>(), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var alias in option?["aliases"]?.AsArray() ?? [])
            {
                if (string.Equals(alias?.GetValue<string>(), expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        foreach (var command in node?["commands"]?.AsArray() ?? [])
        {
            if (ContainsOption(command, expectedName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsArgument(JsonNode? node, string expectedName)
    {
        foreach (var argument in node?["arguments"]?.AsArray() ?? [])
        {
            if (string.Equals(argument?["name"]?.GetValue<string>(), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var command in node?["commands"]?.AsArray() ?? [])
        {
            if (ContainsArgument(command, expectedName))
            {
                return true;
            }
        }

        return false;
    }

    public sealed record LiveAutoToolCase(
        string Framework,
        string ExpectedAnalysisMode,
        string PackageId,
        string Version,
        string CommandName,
        IReadOnlyList<string>? expectedCommands = null,
        IReadOnlyList<string>? expectedOptions = null,
        IReadOnlyList<string>? expectedArguments = null)
    {
        public IReadOnlyList<string> ExpectedCommands { get; } = expectedCommands ?? [];
        public IReadOnlyList<string> ExpectedOptions { get; } = expectedOptions ?? [];
        public IReadOnlyList<string> ExpectedArguments { get; } = expectedArguments ?? [];

        public override string ToString()
            => $"{ExpectedAnalysisMode}: {PackageId} {Version}";
    }
}

