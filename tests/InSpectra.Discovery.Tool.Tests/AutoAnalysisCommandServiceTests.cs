using System.Text.Json.Nodes;
using Xunit;

public sealed class AutoAnalysisCommandServiceTests
{
    [Fact]
    public async Task RunAsync_PreservesNativeSuccess_WhenPreferredModeIsNative()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        var service = new AutoAnalysisCommandService(
            new FakeDescriptorResolver(new ToolAnalysisDescriptor(
                "Sample.Tool",
                "1.2.3",
                "sample",
                "Spectre.Console.Cli",
                "native",
                "confirmed-spectre-console-cli",
                "https://www.nuget.org/packages/Sample.Tool/1.2.3",
                "https://nuget.test/sample.tool.1.2.3.nupkg",
                "https://nuget.test/catalog/sample.tool.1.2.3.json")),
            new FakeNativeRunner((path, _, _, _, _, _, _, _) => WriteResult(path, "success")),
            new FakeHelpRunner((_, _, _, _, _, _, _, _, _, _) => throw new InvalidOperationException("Help fallback should not run.")));

        var exitCode = await service.RunAsync(
            "Sample.Tool",
            "1.2.3",
            outputRoot,
            "batch-001",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("native", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("Spectre.Console.Cli", result["cliFramework"]?.GetValue<string>());
        Assert.Null(result["fallback"]);
    }

    [Fact]
    public async Task RunAsync_FallsBackToHelp_WhenNativeResultIsNotSuccessful()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        string? capturedCommandName = null;

        var service = new AutoAnalysisCommandService(
            new FakeDescriptorResolver(new ToolAnalysisDescriptor(
                "Broken.Tool",
                "0.1.0",
                "broken",
                "System.CommandLine",
                "native",
                "confirmed-spectre-console-cli",
                "https://www.nuget.org/packages/Broken.Tool/0.1.0",
                "https://nuget.test/broken.tool.0.1.0.nupkg",
                "https://nuget.test/catalog/broken.tool.0.1.0.json")),
            new FakeNativeRunner((path, _, _, _, _, _, _, _) => WriteResult(path, "retryable-failure", "unsupported-command")),
            new FakeHelpRunner((path, _, commandName, _, _, _, framework, _, _, _) =>
            {
                capturedCommandName = commandName;
                WriteResult(path, "success", cliFramework: framework);
            }));

        var exitCode = await service.RunAsync(
            "Broken.Tool",
            "0.1.0",
            outputRoot,
            "batch-002",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("broken", capturedCommandName);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("help", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("System.CommandLine", result["cliFramework"]?.GetValue<string>());
        Assert.Equal("native", result["fallback"]?["from"]?.GetValue<string>());
        Assert.Equal("unsupported-command", result["fallback"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_FallsBackToHelp_WhenNativeSuccessDoesNotIncludeOpenCliArtifact()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        string? capturedCommandName = null;

        var service = new AutoAnalysisCommandService(
            new FakeDescriptorResolver(new ToolAnalysisDescriptor(
                "Cake.Tool",
                "6.1.0",
                "dotnet-cake",
                "Spectre.Console.Cli",
                "native",
                "confirmed-spectre-console-cli",
                "https://www.nuget.org/packages/Cake.Tool/6.1.0",
                "https://nuget.test/cake.tool.6.1.0.nupkg",
                "https://nuget.test/catalog/cake.tool.6.1.0.json")),
            new FakeNativeRunner((path, _, _, _, _, _, _, _) => WriteResult(path, "success", includeOpenCliArtifact: false, includeXmlDocArtifact: true)),
            new FakeHelpRunner((path, _, commandName, _, _, _, framework, _, _, _) =>
            {
                capturedCommandName = commandName;
                WriteResult(path, "success", cliFramework: framework);
            }));

        var exitCode = await service.RunAsync(
            "Cake.Tool",
            "6.1.0",
            outputRoot,
            "batch-003",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("dotnet-cake", capturedCommandName);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("help", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("Spectre.Console.Cli", result["cliFramework"]?.GetValue<string>());
        Assert.Equal("native", result["fallback"]?["from"]?.GetValue<string>());
        Assert.Equal("success", result["fallback"]?["disposition"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_PreservesNativeSuccess_WhenHelpFallbackFails_AfterMissingOpenCliArtifact()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");

        var service = new AutoAnalysisCommandService(
            new FakeDescriptorResolver(new ToolAnalysisDescriptor(
                "Cake.Tool",
                "6.1.0",
                "dotnet-cake",
                "Spectre.Console.Cli",
                "native",
                "confirmed-spectre-console-cli",
                "https://www.nuget.org/packages/Cake.Tool/6.1.0",
                "https://nuget.test/cake.tool.6.1.0.nupkg",
                "https://nuget.test/catalog/cake.tool.6.1.0.json")),
            new FakeNativeRunner((path, _, _, _, _, _, _, _) => WriteResult(path, "success", includeOpenCliArtifact: false, includeXmlDocArtifact: true)),
            new FakeHelpRunner((path, _, _, _, _, _, _, _, _, _) => WriteResult(path, "terminal-failure", "help-crawl-failed", includeOpenCliArtifact: false)));

        var exitCode = await service.RunAsync(
            "Cake.Tool",
            "6.1.0",
            outputRoot,
            "batch-004",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("native", result["analysisMode"]?.GetValue<string>());
        Assert.Null(result["fallback"]);
        Assert.Null(result["artifacts"]?["opencliArtifact"]?.GetValue<string>());
        Assert.Equal("xmldoc.xml", result["artifacts"]?["xmldocArtifact"]?.GetValue<string>());
    }

    private static void WriteResult(
        string outputRoot,
        string disposition,
        string? classification = null,
        string? cliFramework = null,
        bool? includeOpenCliArtifact = null,
        bool includeXmlDocArtifact = false)
    {
        var hasOpenCliArtifact = includeOpenCliArtifact ?? string.Equals(disposition, "success", StringComparison.Ordinal);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(outputRoot, "result.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["batchId"] = "batch",
                ["attempt"] = 1,
                ["source"] = "test",
                ["analyzedAt"] = "2026-03-28T12:00:00Z",
                ["disposition"] = disposition,
                ["classification"] = classification,
                ["failureMessage"] = classification,
                ["cliFramework"] = cliFramework,
                ["artifacts"] = new JsonObject
                {
                    ["opencliArtifact"] = hasOpenCliArtifact ? "opencli.json" : null,
                    ["xmldocArtifact"] = includeXmlDocArtifact ? "xmldoc.xml" : null,
                },
            });
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

    private sealed class FakeDescriptorResolver(ToolAnalysisDescriptor descriptor) : IToolAnalysisDescriptorResolver
    {
        public Task<ToolAnalysisDescriptor> ResolveAsync(string packageId, string version, CancellationToken cancellationToken)
            => Task.FromResult(descriptor);
    }

    private sealed class FakeNativeRunner(Action<string, string, string, string, int, string, int, int> handler) : IAutoAnalysisNativeRunner
    {
        public Task RunAsync(string packageId, string version, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        {
            handler(outputRoot, packageId, version, batchId, attempt, source, installTimeoutSeconds, commandTimeoutSeconds);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHelpRunner(Action<string, string, string?, string, string, int, string?, int, int, int> handler) : IAutoAnalysisHelpRunner
    {
        public Task RunAsync(string packageId, string version, string? commandName, string outputRoot, string batchId, int attempt, string source, string? cliFramework, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        {
            handler(outputRoot, packageId, commandName, version, batchId, attempt, cliFramework, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds);
            return Task.CompletedTask;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"inspectra-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string GetPath(string relativePath) => System.IO.Path.Combine(Path, relativePath);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
