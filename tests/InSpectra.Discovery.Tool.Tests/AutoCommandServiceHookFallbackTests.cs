namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Auto.Services;
using InSpectra.Discovery.Tool.Analysis.Auto.Runners;
using InSpectra.Discovery.Tool.Analysis.Tools;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class AutoCommandServiceHookFallbackTests
{
    [Fact]
    public async Task RunAsync_FallsBackToStatic_WhenHookUpgradeFails_ForConfirmedStaticDescriptor()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        string? staticCommandName = null;

        var service = new AutoCommandService(
            new FakeDescriptorResolver(new ToolDescriptor(
                "Hooked.Tool",
                "3.4.5",
                "hooked",
                "System.CommandLine",
                "static",
                "confirmed-static-analysis-framework",
                "https://www.nuget.org/packages/Hooked.Tool/3.4.5",
                "https://nuget.test/hooked.tool.3.4.5.nupkg",
                "https://nuget.test/catalog/hooked.tool.3.4.5.json")),
            new ThrowingNativeRunner(),
            new ThrowingHelpRunner(),
            new ThrowingCliFxRunner(),
            new FakeStaticRunner((path, _, commandName, _, _, _, cliFramework, _, _, _, _) =>
            {
                staticCommandName = commandName;
                WriteResult(path, "success", cliFramework: cliFramework);
            }),
            new FakeHookRunner((path, _, commandName, _, _, _, cliFramework, _, _, _, _) =>
            {
                Assert.Equal("hooked", commandName);
                Assert.Equal("System.CommandLine", cliFramework);
                WriteResult(path, "retryable-failure", "hook-no-assembly-loaded", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }));

        var exitCode = await service.RunAsync(
            "Hooked.Tool",
            "3.4.5",
            outputRoot,
            "batch-006",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("hooked", staticCommandName);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("static", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("System.CommandLine", result["cliFramework"]?.GetValue<string>());
        Assert.Equal("static", result["analysisSelection"]?["preferredMode"]?.GetValue<string>());
        Assert.Equal("static", result["analysisSelection"]?["selectedMode"]?.GetValue<string>());
        Assert.Equal("hook", result["fallback"]?["from"]?.GetValue<string>());
        Assert.Equal("hook-no-assembly-loaded", result["fallback"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_PreservesHookFailure_WhenStaticFallbackAlsoFails()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        var staticRunnerCalled = false;

        var service = new AutoCommandService(
            new FakeDescriptorResolver(new ToolDescriptor(
                "Hooked.Tool",
                "3.4.6",
                "hooked",
                "System.CommandLine",
                "static",
                "confirmed-static-analysis-framework",
                "https://www.nuget.org/packages/Hooked.Tool/3.4.6",
                "https://nuget.test/hooked.tool.3.4.6.nupkg",
                "https://nuget.test/catalog/hooked.tool.3.4.6.json")),
            new ThrowingNativeRunner(),
            new ThrowingHelpRunner(),
            new ThrowingCliFxRunner(),
            new FakeStaticRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                staticRunnerCalled = true;
                WriteResult(path, "retryable-failure", "static-crawl-failed", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }),
            new FakeHookRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                WriteResult(path, "retryable-failure", "hook-target-unhandled-exception", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }));

        var exitCode = await service.RunAsync(
            "Hooked.Tool",
            "3.4.6",
            outputRoot,
            "batch-007",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(staticRunnerCalled);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("retryable-failure", result["disposition"]?.GetValue<string>());
        Assert.Equal("hook", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("hook-target-unhandled-exception", result["classification"]?.GetValue<string>());
        Assert.Null(result["fallback"]);
    }

    private static void WriteResult(
        string outputRoot,
        string disposition,
        string? classification = null,
        string? cliFramework = null,
        bool? includeOpenCliArtifact = null)
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
                    ["xmldocArtifact"] = null,
                },
            });
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

    private sealed class FakeDescriptorResolver(ToolDescriptor descriptor) : IToolDescriptorResolver
    {
        public Task<ToolDescriptor> ResolveAsync(string packageId, string version, CancellationToken cancellationToken)
            => Task.FromResult(descriptor);
    }

    private sealed class ThrowingNativeRunner : IAutoNativeRunner
    {
        public Task RunAsync(string packageId, string version, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Native runner should not run.");
    }

    private sealed class ThrowingHelpRunner : IAutoHelpRunner
    {
        public Task RunAsync(string packageId, string version, string? commandName, string outputRoot, string batchId, int attempt, string source, string? cliFramework, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Help runner should not run.");
    }

    private sealed class ThrowingCliFxRunner : IAutoCliFxRunner
    {
        public Task RunAsync(string packageId, string version, string? commandName, string? cliFramework, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
            => throw new InvalidOperationException("CliFx runner should not run.");
    }

    private sealed class FakeStaticRunner(Action<string, string, string?, string, string, int, string?, int, int, int, string> handler) : IAutoStaticRunner
    {
        public Task RunAsync(string packageId, string version, string? commandName, string? cliFramework, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        {
            handler(outputRoot, packageId, commandName, version, batchId, attempt, cliFramework, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, source);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHookRunner(Action<string, string, string?, string, string, int, string?, int, int, int, string> handler) : IAutoHookRunner
    {
        public Task RunAsync(string packageId, string version, string? commandName, string? cliFramework, string outputRoot, string batchId, int attempt, string source, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        {
            handler(outputRoot, packageId, commandName, version, batchId, attempt, cliFramework, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, source);
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
