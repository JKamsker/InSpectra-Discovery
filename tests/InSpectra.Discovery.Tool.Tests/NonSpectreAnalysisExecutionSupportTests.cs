using System.Text.Json.Nodes;
using Xunit;

public sealed class NonSpectreAnalysisExecutionSupportTests
{
    [Fact]
    public async Task RunQuietAsync_Uses_Default_Framework_And_Writes_Result()
    {
        using var tempDirectory = new TemporaryDirectory();
        var resultPath = Path.Combine(tempDirectory.Path, "output", "result.json");

        var exitCode = await NonSpectreAnalysisExecutionSupport.RunQuietAsync(
            runtime: new ToolCommandRuntime(),
            definition: new NonSpectreAnalysisExecutionDefinition(
                AnalysisMode: "static",
                TempRootPrefix: "inspectra-static",
                TimeoutLabel: "Static analysis",
                DefaultCliFramework: "CommandLineParser",
                InitializeCoverage: true),
            bootstrapAsync: (_, _, _, _, _) => Task.FromResult(
                new NonSpectreAnalysisBootstrapResult(
                    PackageContentUrl: "https://example.test/package.nupkg",
                    CommandInfo: new ResolvedToolCommandInfo("demo", "demo.dll", ".config/dotnet-tools.json"))),
            analyzeAsync: (request, _) =>
            {
                request.Result["disposition"] = "success";
                request.Result["classification"] = "static-crawl";
                return Task.CompletedTask;
            },
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: null,
            cliFramework: null,
            outputRoot: Path.Combine(tempDirectory.Path, "output"),
            batchId: "batch-1",
            attempt: 1,
            source: "unit-test",
            installTimeoutSeconds: 30,
            analysisTimeoutSeconds: 60,
            commandTimeoutSeconds: 10,
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(resultPath));

        var result = JsonNode.Parse(File.ReadAllText(resultPath))?.AsObject()
            ?? throw new InvalidOperationException("Result artifact was not a JSON object.");
        Assert.Equal("static", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("CommandLineParser", result["cliFramework"]?.GetValue<string>());
        Assert.Equal("demo", result["command"]?.GetValue<string>());
        Assert.True(result.ContainsKey("coverage"));
    }

    [Fact]
    public async Task RunQuietAsync_Applies_Bootstrap_Command_Missing_Failure()
    {
        using var tempDirectory = new TemporaryDirectory();
        var analyzeCalled = false;

        await NonSpectreAnalysisExecutionSupport.RunQuietAsync(
            runtime: new ToolCommandRuntime(),
            definition: new NonSpectreAnalysisExecutionDefinition("help", "inspectra-help", "Help analysis"),
            bootstrapAsync: (_, _, _, _, _) => Task.FromResult(
                new NonSpectreAnalysisBootstrapResult(
                    PackageContentUrl: "https://example.test/package.nupkg",
                    CommandInfo: new ResolvedToolCommandInfo(null, null, null))),
            analyzeAsync: (_, _) =>
            {
                analyzeCalled = true;
                return Task.CompletedTask;
            },
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: null,
            cliFramework: null,
            outputRoot: Path.Combine(tempDirectory.Path, "output"),
            batchId: "batch-1",
            attempt: 1,
            source: "unit-test",
            installTimeoutSeconds: 30,
            analysisTimeoutSeconds: 60,
            commandTimeoutSeconds: 10,
            cancellationToken: CancellationToken.None);

        var resultPath = Path.Combine(tempDirectory.Path, "output", "result.json");
        var result = JsonNode.Parse(File.ReadAllText(resultPath))?.AsObject()
            ?? throw new InvalidOperationException("Result artifact was not a JSON object.");
        Assert.False(analyzeCalled);
        Assert.Equal("bootstrap", result["phase"]?.GetValue<string>());
        Assert.Equal("tool-command-missing", result["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunQuietAsync_Applies_Analysis_Timeout_Failure()
    {
        using var tempDirectory = new TemporaryDirectory();

        await NonSpectreAnalysisExecutionSupport.RunQuietAsync(
            runtime: new ToolCommandRuntime(),
            definition: new NonSpectreAnalysisExecutionDefinition("clifx", "inspectra-clifx", "CliFx analysis"),
            bootstrapAsync: (_, _, _, _, _) => Task.FromResult(
                new NonSpectreAnalysisBootstrapResult(
                    PackageContentUrl: "https://example.test/package.nupkg",
                    CommandInfo: new ResolvedToolCommandInfo("demo", null, null))),
            analyzeAsync: async (_, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            },
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: null,
            cliFramework: null,
            outputRoot: Path.Combine(tempDirectory.Path, "output"),
            batchId: "batch-1",
            attempt: 1,
            source: "unit-test",
            installTimeoutSeconds: 30,
            analysisTimeoutSeconds: 0,
            commandTimeoutSeconds: 10,
            cancellationToken: CancellationToken.None);

        var resultPath = Path.Combine(tempDirectory.Path, "output", "result.json");
        var result = JsonNode.Parse(File.ReadAllText(resultPath))?.AsObject()
            ?? throw new InvalidOperationException("Result artifact was not a JSON object.");
        Assert.Equal("analysis", result["phase"]?.GetValue<string>());
        Assert.Equal("analysis-timeout", result["classification"]?.GetValue<string>());
        Assert.Contains("CliFx analysis exceeded the overall timeout", result["failureMessage"]?.GetValue<string>());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"inspectra-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
