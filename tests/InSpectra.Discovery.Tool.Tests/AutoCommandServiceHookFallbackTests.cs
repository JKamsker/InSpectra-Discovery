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
        var helpRunnerCalled = false;

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
            new FakeHelpRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                helpRunnerCalled = true;
                WriteResult(path, "retryable-failure", "help-crawl-empty", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }),
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
        Assert.True(helpRunnerCalled);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("retryable-failure", result["disposition"]?.GetValue<string>());
        Assert.Equal("hook", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("hook-target-unhandled-exception", result["classification"]?.GetValue<string>());
        Assert.Null(result["fallback"]);
    }

    [Fact]
    public async Task RunAsync_FallsBackToHelp_WhenStaticFallbackStillCannotPublish()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        var staticRunnerCalled = false;
        var helpRunnerCalled = false;

        var service = new AutoCommandService(
            new FakeDescriptorResolver(new ToolDescriptor(
                "Hooked.Tool",
                "3.4.7",
                "hooked",
                "System.CommandLine",
                "static",
                "confirmed-static-analysis-framework",
                "https://www.nuget.org/packages/Hooked.Tool/3.4.7",
                "https://nuget.test/hooked.tool.3.4.7.nupkg",
                "https://nuget.test/catalog/hooked.tool.3.4.7.json")),
            new ThrowingNativeRunner(),
            new FakeHelpRunner((path, _, commandName, _, _, _, cliFramework, _, _, _, _) =>
            {
                helpRunnerCalled = true;
                WriteResult(path, "success", "help-crawl", cliFramework: cliFramework, includeOpenCliArtifact: true, command: commandName);
            }),
            new ThrowingCliFxRunner(),
            new FakeStaticRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                staticRunnerCalled = true;
                WriteResult(path, "terminal-failure", "invalid-opencli-artifact", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }),
            new FakeHookRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                WriteResult(path, "retryable-failure", "hook-no-assembly-loaded", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }));

        var exitCode = await service.RunAsync(
            "Hooked.Tool",
            "3.4.7",
            outputRoot,
            "batch-008",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(staticRunnerCalled);
        Assert.True(helpRunnerCalled);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("help", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("help-crawl", result["classification"]?.GetValue<string>());
        Assert.Equal("System.CommandLine", result["cliFramework"]?.GetValue<string>());
        Assert.Equal("static", result["analysisSelection"]?["preferredMode"]?.GetValue<string>());
        Assert.Null(result["analysisSelection"]?["selectedFramework"]);
        Assert.Equal("help", result["analysisSelection"]?["selectedMode"]?.GetValue<string>());
        Assert.Equal("static", result["fallback"]?["from"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", result["fallback"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_FallsBackToHelp_WhenStaticFallbackEndsWithTerminalParserFailure()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        var staticRunnerCalled = false;
        var helpRunnerCalled = false;

        var service = new AutoCommandService(
            new FakeDescriptorResolver(new ToolDescriptor(
                "Hooked.Tool",
                "3.4.8",
                "hooked",
                "System.CommandLine",
                "static",
                "confirmed-static-analysis-framework",
                "https://www.nuget.org/packages/Hooked.Tool/3.4.8",
                "https://nuget.test/hooked.tool.3.4.8.nupkg",
                "https://nuget.test/catalog/hooked.tool.3.4.8.json")),
            new ThrowingNativeRunner(),
            new FakeHelpRunner((path, _, commandName, _, _, _, cliFramework, _, _, _, _) =>
            {
                helpRunnerCalled = true;
                WriteResult(path, "success", "help-crawl", cliFramework: cliFramework, includeOpenCliArtifact: true, command: commandName);
            }),
            new ThrowingCliFxRunner(),
            new FakeStaticRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                staticRunnerCalled = true;
                WriteResult(path, "terminal-failure", "custom-parser", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }),
            new FakeHookRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                WriteResult(path, "retryable-failure", "hook-no-assembly-loaded", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }));

        var exitCode = await service.RunAsync(
            "Hooked.Tool",
            "3.4.8",
            outputRoot,
            "batch-009",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(staticRunnerCalled);
        Assert.True(helpRunnerCalled);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("help", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("help-crawl", result["classification"]?.GetValue<string>());
        Assert.Null(result["analysisSelection"]?["selectedFramework"]);
        Assert.Equal("static", result["fallback"]?["from"]?.GetValue<string>());
        Assert.Equal("custom-parser", result["fallback"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_FallsBackToStatic_WhenHookReportsSuccess_WithInvalidOpenCliArtifact()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        var staticRunnerCalled = false;

        var service = new AutoCommandService(
            new FakeDescriptorResolver(new ToolDescriptor(
                "Hooked.Tool",
                "3.4.9",
                "hooked",
                "Microsoft.Extensions.CommandLineUtils",
                "static",
                "confirmed-static-analysis-framework",
                "https://www.nuget.org/packages/Hooked.Tool/3.4.9",
                "https://nuget.test/hooked.tool.3.4.9.nupkg",
                "https://nuget.test/catalog/hooked.tool.3.4.9.json")),
            new ThrowingNativeRunner(),
            new ThrowingHelpRunner(),
            new ThrowingCliFxRunner(),
            new FakeStaticRunner((path, _, commandName, _, _, _, cliFramework, _, _, _, _) =>
            {
                staticRunnerCalled = true;
                WriteResult(path, "success", "static-crawl", cliFramework: cliFramework, includeOpenCliArtifact: true, command: commandName);
                WriteOpenCli(path, CreateValidOpenCliDocument(commandName ?? "hooked", "3.4.9"));
            }),
            new FakeHookRunner((path, _, commandName, _, _, _, cliFramework, _, _, _, _) =>
            {
                WriteResult(path, "success", "startup-hook", cliFramework: cliFramework, includeOpenCliArtifact: true, command: commandName);
                WriteOpenCli(path, CreateEmptyOpenCliDocument(commandName ?? "hooked", "3.4.9"));
            }));

        var exitCode = await service.RunAsync(
            "Hooked.Tool",
            "3.4.9",
            outputRoot,
            "batch-010",
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
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("static", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("static-crawl", result["classification"]?.GetValue<string>());
        Assert.Equal("hook", result["fallback"]?["from"]?.GetValue<string>());
        Assert.Equal("invalid-success-artifact", result["fallback"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_PrefersTerminalStaticFallback_WhenHookArtifactFailureCannotRecover()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        var staticRunnerCalled = false;
        var helpRunnerCalled = false;

        var service = new AutoCommandService(
            new FakeDescriptorResolver(new ToolDescriptor(
                "Hooked.Tool",
                "3.5.0",
                "hooked",
                "McMaster.Extensions.CommandLineUtils",
                "static",
                "confirmed-static-analysis-framework",
                "https://www.nuget.org/packages/Hooked.Tool/3.5.0",
                "https://nuget.test/hooked.tool.3.5.0.nupkg",
                "https://nuget.test/catalog/hooked.tool.3.5.0.json")),
            new ThrowingNativeRunner(),
            new FakeHelpRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                helpRunnerCalled = true;
                WriteResult(path, "terminal-failure", "help-crawl-empty", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }),
            new ThrowingCliFxRunner(),
            new FakeStaticRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                staticRunnerCalled = true;
                WriteResult(path, "terminal-failure", "custom-parser-no-attributes", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }),
            new FakeHookRunner((path, _, commandName, _, _, _, cliFramework, _, _, _, _) =>
            {
                WriteResult(path, "success", "startup-hook", cliFramework: cliFramework, includeOpenCliArtifact: true, command: commandName);
                WriteOpenCli(path, CreateEmptyOpenCliDocument(commandName ?? "hooked", "3.5.0"));
            }));

        var exitCode = await service.RunAsync(
            "Hooked.Tool",
            "3.5.0",
            outputRoot,
            "batch-011",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(staticRunnerCalled);
        Assert.True(helpRunnerCalled);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("terminal-failure", result["disposition"]?.GetValue<string>());
        Assert.Equal("static", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("custom-parser-no-attributes", result["classification"]?.GetValue<string>());
        Assert.Equal("hook", result["fallback"]?["from"]?.GetValue<string>());
        Assert.Equal("invalid-success-artifact", result["fallback"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_FallsBackToLaterStaticProvider_WhenEarlierFrameworkAttemptsFail()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        var seenAttempts = new List<string>();

        var service = new AutoCommandService(
            new FakeDescriptorResolver(new ToolDescriptor(
                "Mixed.Tool",
                "3.5.1",
                "mixed",
                "System.CommandLine + CommandLineParser",
                "static",
                "confirmed-static-analysis-framework",
                "https://www.nuget.org/packages/Mixed.Tool/3.5.1",
                "https://nuget.test/mixed.tool.3.5.1.nupkg",
                "https://nuget.test/catalog/mixed.tool.3.5.1.json")),
            new ThrowingNativeRunner(),
            new ThrowingHelpRunner(),
            new ThrowingCliFxRunner(),
            new FakeStaticRunner((path, _, commandName, _, _, _, cliFramework, _, _, _, _) =>
            {
                seenAttempts.Add($"static:{cliFramework}");
                if (string.Equals(cliFramework, "System.CommandLine", StringComparison.Ordinal))
                {
                    WriteResult(path, "retryable-failure", "static-scl-failed", cliFramework: cliFramework, includeOpenCliArtifact: false);
                    return;
                }

                WriteResult(path, "success", "static-crawl", cliFramework: cliFramework, includeOpenCliArtifact: true, command: commandName);
            }),
            new FakeHookRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                seenAttempts.Add($"hook:{cliFramework}");
                WriteResult(path, "retryable-failure", "hook-scl-failed", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }));

        var exitCode = await service.RunAsync(
            "Mixed.Tool",
            "3.5.1",
            outputRoot,
            "batch-012",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["hook:System.CommandLine", "static:System.CommandLine", "static:CommandLineParser"],
            seenAttempts);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("static", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("CommandLineParser", result["cliFramework"]?.GetValue<string>());
        Assert.Equal("CommandLineParser", result["analysisSelection"]?["selectedFramework"]?.GetValue<string>());
        Assert.Equal("static", result["fallback"]?["from"]?.GetValue<string>());
        Assert.Equal("static-scl-failed", result["fallback"]?["classification"]?.GetValue<string>());

        var attempts = result["analysisSelection"]?["attempts"]?.AsArray();
        Assert.NotNull(attempts);
        Assert.Equal(3, attempts!.Count);
    }

    [Fact]
    public async Task RunAsync_FallsBackToLaterHookProvider_WhenEarlierCommandLineUtilsProviderFails()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var outputRoot = tempDirectory.GetPath("analysis");
        var seenAttempts = new List<string>();

        var service = new AutoCommandService(
            new FakeDescriptorResolver(new ToolDescriptor(
                "Hooked.Tool",
                "3.5.2",
                "hooked",
                "Microsoft.Extensions.CommandLineUtils + McMaster.Extensions.CommandLineUtils",
                "static",
                "confirmed-static-analysis-framework",
                "https://www.nuget.org/packages/Hooked.Tool/3.5.2",
                "https://nuget.test/hooked.tool.3.5.2.nupkg",
                "https://nuget.test/catalog/hooked.tool.3.5.2.json")),
            new ThrowingNativeRunner(),
            new ThrowingHelpRunner(),
            new ThrowingCliFxRunner(),
            new FakeStaticRunner((path, _, _, _, _, _, cliFramework, _, _, _, _) =>
            {
                seenAttempts.Add($"static:{cliFramework}");
                WriteResult(path, "retryable-failure", $"static-{cliFramework}-failed", cliFramework: cliFramework, includeOpenCliArtifact: false);
            }),
            new FakeHookRunner((path, _, commandName, _, _, _, cliFramework, _, _, _, _) =>
            {
                seenAttempts.Add($"hook:{cliFramework}");
                if (string.Equals(cliFramework, "McMaster.Extensions.CommandLineUtils", StringComparison.Ordinal))
                {
                    WriteResult(path, "retryable-failure", "hook-mcmaster-failed", cliFramework: cliFramework, includeOpenCliArtifact: false);
                    return;
                }

                WriteResult(path, "success", "startup-hook", cliFramework: cliFramework, includeOpenCliArtifact: true, command: commandName);
            }));

        var exitCode = await service.RunAsync(
            "Hooked.Tool",
            "3.5.2",
            outputRoot,
            "batch-013",
            1,
            "test",
            300,
            600,
            60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            [
                "hook:McMaster.Extensions.CommandLineUtils",
                "static:McMaster.Extensions.CommandLineUtils",
                "hook:Microsoft.Extensions.CommandLineUtils",
            ],
            seenAttempts);

        var result = ParseJsonObject(Path.Combine(outputRoot, "result.json"));
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("hook", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("Microsoft.Extensions.CommandLineUtils", result["cliFramework"]?.GetValue<string>());
        Assert.Equal("Microsoft.Extensions.CommandLineUtils", result["analysisSelection"]?["selectedFramework"]?.GetValue<string>());
        Assert.Equal("static", result["fallback"]?["from"]?.GetValue<string>());
        Assert.Equal("static-McMaster.Extensions.CommandLineUtils-failed", result["fallback"]?["classification"]?.GetValue<string>());
    }

    private static void WriteResult(
        string outputRoot,
        string disposition,
        string? classification = null,
        string? cliFramework = null,
        bool? includeOpenCliArtifact = null,
        string? command = null)
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
                ["command"] = command,
                ["cliFramework"] = cliFramework,
                ["artifacts"] = new JsonObject
                {
                    ["opencliArtifact"] = hasOpenCliArtifact ? "opencli.json" : null,
                    ["xmldocArtifact"] = null,
                },
            });

        if (hasOpenCliArtifact && !File.Exists(Path.Combine(outputRoot, "opencli.json")))
        {
            WriteOpenCli(outputRoot, CreateValidOpenCliDocument(command ?? "sample", "1.2.3"));
        }
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

    private static void WriteOpenCli(string outputRoot, JsonObject document)
        => RepositoryPathResolver.WriteJsonFile(Path.Combine(outputRoot, "opencli.json"), document);

    private static JsonObject CreateValidOpenCliDocument(string commandName, string version)
        => new()
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = commandName,
                ["version"] = version,
            },
            ["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--help",
                },
            },
        };

    private static JsonObject CreateEmptyOpenCliDocument(string commandName, string version)
        => new()
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = commandName,
                ["version"] = version,
            },
        };

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

    private sealed class FakeHelpRunner(Action<string, string, string?, string, string, int, string?, int, int, int, string> handler) : IAutoHelpRunner
    {
        public Task RunAsync(string packageId, string version, string? commandName, string outputRoot, string batchId, int attempt, string source, string? cliFramework, int installTimeoutSeconds, int analysisTimeoutSeconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
        {
            handler(outputRoot, packageId, commandName, version, batchId, attempt, cliFramework, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, source);
            return Task.CompletedTask;
        }
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
