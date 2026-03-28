using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

[CollectionDefinition("LiveToolAnalysis", DisableParallelization = true)]
public sealed class LiveToolAnalysisCollectionDefinition
{
}

[Collection("LiveToolAnalysis")]
public sealed class ToolHelpAnalysisServiceLiveTests
{
    private const string EnableEnvVar = "INSPECTRA_DISCOVERY_LIVE_HELP_TESTS";
    private readonly ITestOutputHelper _output;

    public ToolHelpAnalysisServiceLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<LiveToolCase> Cases()
        => new()
        {
            new LiveToolCase("CliFx", "Husky", "0.9.1", "husky", expectedCommands: ["add", "install"]),
            new LiveToolCase("Argu", "Paket", "10.3.1", "paket", expectedCommands: ["add", "install"]),
            new LiveToolCase("McMaster", "dotnet-serve", "1.10.194", "dotnet-serve", expectedOptions: ["--directory", "--port"]),
            new LiveToolCase("Spectre.Console.Cli", "Cake.Tool", "6.1.0", "dotnet-cake", expectedOptions: ["--verbosity"], expectedArguments: ["SCRIPT"]),
            new LiveToolCase("Cocona", "Libplanet.Tools", "5.5.3", "planet", expectedCommands: ["key", "tx"]),
            new LiveToolCase("DocoptNet", "coveralls.net", "4.0.1", "csmacnz.Coveralls", expectedOptions: ["--input", "--repoToken"]),
            new LiveToolCase("System.CommandLine", "dotnet-trace", "9.0.652701", "dotnet-trace", expectedCommands: ["collect", "report"]),
            new LiveToolCase("CommandLineParser", "snapx", "10.0.0", "snapx", expectedCommands: ["promote", "pack"]),
            new LiveToolCase("Mono.Options / NDesk.Options", "Pickles.CommandLine", "4.0.3", "pickles", expectedOptions: ["--feature-directory", "--output-directory"]),
            new LiveToolCase("Microsoft.Extensions.CommandLineUtils", "dotnet-version-cli", "3.0.3", "dotnet-version", expectedOptions: ["--output-format", "--project-file"]),
            new LiveToolCase("ConsoleAppFramework", "MessagePack.Generator", "2.5.198", "mpc", expectedOptions: ["-input", "-output"]),
            new LiveToolCase("CommandDotNet", "Squidex.CLI", "13.13.0", "sq", expectedCommands: ["apps", "schemas"]),
            new LiveToolCase("PowerArgs", "DependenSee", "2.2.0", "DependenSee", expectedOptions: ["--help", "--include-packages"], expectedArguments: ["SOURCEFOLDER", "OUTPUTPATH"]),
        };

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Live")]
    public async Task RunAsync_Synthesizes_OpenCli_For_Real_World_Tools(LiveToolCase testCase)
    {
        if (!ShouldRun())
        {
            return;
        }

        var service = new ToolHelpAnalysisService();
        var outputRoot = Path.Combine(Path.GetTempPath(), "inspectra-live-help", Guid.NewGuid().ToString("N"));

        try
        {
            var exitCode = await service.RunAsync(
                testCase.PackageId,
                testCase.Version,
                testCase.CommandName,
                outputRoot,
                batchId: $"live-{testCase.Framework}",
                attempt: 1,
                source: "live-help-test",
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

            _output.WriteLine($"{testCase.PackageId} {testCase.Version} succeeded.");
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

    public sealed record LiveToolCase(
        string Framework,
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
            => $"{Framework}: {PackageId} {Version}";
    }
}
