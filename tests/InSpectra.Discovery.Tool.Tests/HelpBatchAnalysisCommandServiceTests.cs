using System.Text.Json.Nodes;
using Xunit;

public sealed class HelpBatchAnalysisCommandServiceTests
{
    [Fact]
    public async Task RunAsync_WritesPromotionReadyExpectedPlan()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json"),
            new JsonObject
            {
                ["generatedAtUtc"] = "2026-03-28T00:00:00Z",
                ["packageCount"] = 1,
                ["packages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["totalDownloads"] = 1234,
                        ["packageUrl"] = "https://www.nuget.org/packages/Sample.Tool/1.2.3",
                        ["packageContentUrl"] = "https://nuget.test/sample.tool.1.2.3.nupkg",
                        ["catalogEntryUrl"] = "https://nuget.test/catalog/sample.tool.1.2.3.json",
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "plans", "help-batch.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["batchId"] = "help-batch-001",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["version"] = "1.2.3",
                        ["command"] = "sample",
                        ["cliFramework"] = "System.CommandLine",
                    },
                },
            });

        var runner = new FakeHelpBatchAnalysisRunner((item, outputRoot, batchId, source, timeouts) =>
        {
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = item.PackageId,
                    ["version"] = item.Version,
                    ["batchId"] = batchId,
                    ["attempt"] = item.Attempt,
                    ["source"] = source,
                    ["cliFramework"] = item.CliFramework,
                    ["disposition"] = "success",
                    ["packageUrl"] = "https://www.nuget.org/packages/Sample.Tool/1.2.3",
                    ["packageContentUrl"] = "https://api.nuget.org/v3-flatcontainer/sample.tool/1.2.3/sample.tool.1.2.3.nupkg",
                    ["catalogEntryUrl"] = "https://api.nuget.org/v3/catalog0/data/sample.tool.1.2.3.json",
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                        ["crawlArtifact"] = "crawl.json",
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "opencli.json"),
                new JsonObject
                {
                    ["opencli"] = "0.1-draft",
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "--verbose",
                        },
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "crawl.json"),
                new JsonObject
                {
                    ["commands"] = new JsonArray(),
                });
            return 0;
        });

        var service = new HelpBatchAnalysisCommandService(
            runner,
            new FakeCliFxBatchAnalysisRunner((_, _, _, _, _) => throw new InvalidOperationException("CliFx runner should not run.")));
        var exitCode = await service.RunAsync(
            repositoryRoot,
            "plans/help-batch.json",
            "artifacts/help-batch",
            batchId: null,
            source: "help-index-batch",
            targetBranch: "main",
            installTimeoutSeconds: 300,
            analysisTimeoutSeconds: 600,
            commandTimeoutSeconds: 60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Single(runner.Invocations);
        Assert.Equal("help-batch-001", runner.Invocations[0].BatchId);
        Assert.Equal("help-index-batch", runner.Invocations[0].Source);
        Assert.Equal(300, runner.Invocations[0].Timeouts.InstallTimeoutSeconds);

        var expected = ParseJsonObject(Path.Combine(repositoryRoot, "artifacts", "help-batch", "plan", "expected.json"));
        Assert.Equal("help-batch-001", expected["batchId"]?.GetValue<string>());
        Assert.Equal("plans/help-batch.json", expected["sourcePlanPath"]?.GetValue<string>());
        Assert.Equal("main", expected["targetBranch"]?.GetValue<string>());
        Assert.Equal(1, expected["selectedCount"]?.GetValue<int>());
        Assert.Equal(0, expected["skippedCount"]?.GetValue<int>());

        var expectedItem = expected["items"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one item.");
        Assert.Equal("help", expectedItem["analysisMode"]?.GetValue<string>());
        Assert.Equal("System.CommandLine", expectedItem["cliFramework"]?.GetValue<string>());
        Assert.Equal(1234L, expectedItem["totalDownloads"]?.GetValue<long>());
        Assert.Equal("analysis-sample.tool-1.2.3-sample", expectedItem["artifactName"]?.GetValue<string>());
        Assert.Equal("https://api.nuget.org/v3-flatcontainer/sample.tool/1.2.3/sample.tool.1.2.3.nupkg", expectedItem["packageContentUrl"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorWhenAnyItemFails()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "plans", "help-batch.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["batchId"] = "help-batch-002",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Broken.Tool",
                        ["version"] = "0.1.0",
                        ["cliFramework"] = "CliFx",
                    },
                },
            });

        var runner = new FakeHelpBatchAnalysisRunner((item, outputRoot, batchId, source, timeouts) =>
        {
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = item.PackageId,
                    ["version"] = item.Version,
                    ["batchId"] = batchId,
                    ["attempt"] = item.Attempt,
                    ["source"] = source,
                    ["disposition"] = "retryable-failure",
                    ["failureMessage"] = "boom",
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = null,
                    },
                });
            return 0;
        });

        var service = new HelpBatchAnalysisCommandService(
            runner,
            new FakeCliFxBatchAnalysisRunner((_, _, _, _, _) => throw new InvalidOperationException("CliFx runner should not run.")));
        var exitCode = await service.RunAsync(
            repositoryRoot,
            "plans/help-batch.json",
            "artifacts/help-batch",
            batchId: null,
            source: "help-index-batch",
            targetBranch: "main",
            installTimeoutSeconds: 300,
            analysisTimeoutSeconds: 600,
            commandTimeoutSeconds: 60,
            json: true,
            CancellationToken.None);

        Assert.Equal(1, exitCode);

        var expected = ParseJsonObject(Path.Combine(repositoryRoot, "artifacts", "help-batch", "plan", "expected.json"));
        var expectedItem = expected["items"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one item.");
        Assert.Equal("CliFx", expectedItem["cliFramework"]?.GetValue<string>());
        Assert.Equal("https://www.nuget.org/packages/Broken.Tool/0.1.0", expectedItem["packageUrl"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_SkipsItemsThatAreNotConfiguredForHelpAnalysis()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "plans", "help-batch.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["batchId"] = "help-batch-003",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Cake.Tool",
                        ["version"] = "6.1.0",
                        ["command"] = "dotnet-cake",
                        ["cliFramework"] = "Spectre.Console.Cli",
                        ["analysisMode"] = "native",
                    },
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["version"] = "1.2.3",
                        ["command"] = "sample",
                        ["cliFramework"] = "System.CommandLine",
                    },
                },
            });

        var runner = new FakeHelpBatchAnalysisRunner((item, outputRoot, batchId, source, timeouts) =>
        {
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = item.PackageId,
                    ["version"] = item.Version,
                    ["batchId"] = batchId,
                    ["attempt"] = item.Attempt,
                    ["source"] = source,
                    ["cliFramework"] = item.CliFramework,
                    ["disposition"] = "success",
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                        ["crawlArtifact"] = "crawl.json",
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "opencli.json"),
                new JsonObject
                {
                    ["opencli"] = "0.1-draft",
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "--verbose",
                        },
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "crawl.json"),
                new JsonObject
                {
                    ["commands"] = new JsonArray(),
                });
            return 0;
        });

        var service = new HelpBatchAnalysisCommandService(
            runner,
            new FakeCliFxBatchAnalysisRunner((_, _, _, _, _) => throw new InvalidOperationException("CliFx runner should not run.")));
        var exitCode = await service.RunAsync(
            repositoryRoot,
            "plans/help-batch.json",
            "artifacts/help-batch",
            batchId: null,
            source: "help-index-batch",
            targetBranch: "main",
            installTimeoutSeconds: 300,
            analysisTimeoutSeconds: 600,
            commandTimeoutSeconds: 60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Single(runner.Invocations);
        Assert.Equal("Sample.Tool", runner.Invocations[0].PackageId);

        var expected = ParseJsonObject(Path.Combine(repositoryRoot, "artifacts", "help-batch", "plan", "expected.json"));
        Assert.Equal(1, expected["selectedCount"]?.GetValue<int>());
        Assert.Equal(1, expected["skippedCount"]?.GetValue<int>());

        var skipped = expected["skipped"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one skipped item.");
        Assert.Equal("Cake.Tool", skipped["packageId"]?.GetValue<string>());
        Assert.Equal("native", skipped["analysisMode"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_RoutesCliFxItems_ToCliFxRunner()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "plans", "help-batch.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["batchId"] = "help-batch-004",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "CliFx.Tool",
                        ["version"] = "2.0.0",
                        ["command"] = "clifx-tool",
                        ["cliFramework"] = "CliFx",
                        ["analysisMode"] = "clifx",
                    },
                },
            });

        var helpRunner = new FakeHelpBatchAnalysisRunner((_, _, _, _, _) => throw new InvalidOperationException("Help runner should not run."));
        var cliFxRunner = new FakeCliFxBatchAnalysisRunner((item, outputRoot, batchId, source, timeouts) =>
        {
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = item.PackageId,
                    ["version"] = item.Version,
                    ["batchId"] = batchId,
                    ["attempt"] = item.Attempt,
                    ["source"] = source,
                    ["cliFramework"] = item.CliFramework,
                    ["disposition"] = "success",
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                        ["crawlArtifact"] = "crawl.json",
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "opencli.json"),
                new JsonObject
                {
                    ["opencli"] = "0.1-draft",
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "--verbose",
                        },
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "crawl.json"),
                new JsonObject
                {
                    ["commands"] = new JsonArray(),
                });
            return 0;
        });

        var service = new HelpBatchAnalysisCommandService(helpRunner, cliFxRunner);
        var exitCode = await service.RunAsync(
            repositoryRoot,
            "plans/help-batch.json",
            "artifacts/help-batch",
            batchId: null,
            source: "help-index-batch",
            targetBranch: "main",
            installTimeoutSeconds: 300,
            analysisTimeoutSeconds: 600,
            commandTimeoutSeconds: 60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Empty(helpRunner.Invocations);
        Assert.Single(cliFxRunner.Invocations);
        Assert.Equal("CliFx.Tool", cliFxRunner.Invocations[0].PackageId);

        var expected = ParseJsonObject(Path.Combine(repositoryRoot, "artifacts", "help-batch", "plan", "expected.json"));
        var expectedItem = expected["items"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one item.");
        Assert.Equal("clifx", expectedItem["analysisMode"]?.GetValue<string>());
        Assert.Equal("CliFx", expectedItem["cliFramework"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_DefaultArtifactName_Includes_Command_Discriminator()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "plans", "help-batch.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["batchId"] = "help-batch-005",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["version"] = "1.2.3",
                        ["command"] = "sample",
                    },
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["version"] = "1.2.3",
                        ["command"] = "sample-alt",
                    },
                },
            });

        var runner = new FakeHelpBatchAnalysisRunner((item, outputRoot, batchId, source, timeouts) =>
        {
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = item.PackageId,
                    ["version"] = item.Version,
                    ["batchId"] = batchId,
                    ["attempt"] = item.Attempt,
                    ["source"] = source,
                    ["disposition"] = "success",
                    ["command"] = item.CommandName,
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                        ["crawlArtifact"] = "crawl.json",
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "opencli.json"),
                new JsonObject
                {
                    ["opencli"] = "0.1-draft",
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "--verbose",
                        },
                    },
                });
            RepositoryPathResolver.WriteJsonFile(Path.Combine(outputRoot, "crawl.json"), new JsonObject { ["commands"] = new JsonArray() });
            return 0;
        });

        var service = new HelpBatchAnalysisCommandService(
            runner,
            new FakeCliFxBatchAnalysisRunner((_, _, _, _, _) => throw new InvalidOperationException("CliFx runner should not run.")));
        var exitCode = await service.RunAsync(
            repositoryRoot,
            "plans/help-batch.json",
            "artifacts/help-batch",
            batchId: null,
            source: "help-index-batch",
            targetBranch: "main",
            installTimeoutSeconds: 300,
            analysisTimeoutSeconds: 600,
            commandTimeoutSeconds: 60,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(2, runner.Invocations.Count);
        Assert.NotEqual(runner.Invocations[0].OutputRoot, runner.Invocations[1].OutputRoot);

        var expected = ParseJsonObject(Path.Combine(repositoryRoot, "artifacts", "help-batch", "plan", "expected.json"));
        var artifactNames = expected["items"]!.AsArray()
            .OfType<JsonObject>()
            .Select(item => item["artifactName"]?.GetValue<string>())
            .ToArray();
        Assert.Contains("analysis-sample.tool-1.2.3-sample", artifactNames);
        Assert.Contains("analysis-sample.tool-1.2.3-sample-alt", artifactNames);
    }

    [Fact]
    public async Task RunAsync_Treats_Help_Success_Without_Crawl_Artifact_As_Failure()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "plans", "help-batch.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["batchId"] = "help-batch-006",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Help.Tool",
                        ["version"] = "2.0.0",
                        ["command"] = "help-tool",
                        ["analysisMode"] = "help",
                    },
                },
            });

        var runner = new FakeHelpBatchAnalysisRunner((item, outputRoot, batchId, source, timeouts) =>
        {
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = item.PackageId,
                    ["version"] = item.Version,
                    ["batchId"] = batchId,
                    ["attempt"] = item.Attempt,
                    ["source"] = source,
                    ["analysisMode"] = "help",
                    ["disposition"] = "success",
                    ["command"] = item.CommandName,
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                        ["crawlArtifact"] = "crawl.json",
                    },
                });
            RepositoryPathResolver.WriteJsonFile(Path.Combine(outputRoot, "opencli.json"), new JsonObject { ["opencli"] = "0.1-draft" });
            return 0;
        });

        var service = new HelpBatchAnalysisCommandService(
            runner,
            new FakeCliFxBatchAnalysisRunner((_, _, _, _, _) => throw new InvalidOperationException("CliFx runner should not run.")));
        var exitCode = await service.RunAsync(
            repositoryRoot,
            "plans/help-batch.json",
            "artifacts/help-batch",
            batchId: null,
            source: "help-index-batch",
            targetBranch: "main",
            installTimeoutSeconds: 300,
            analysisTimeoutSeconds: 600,
            commandTimeoutSeconds: 60,
            json: true,
            CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_Treats_Help_Success_With_Empty_OpenCli_Surface_As_Failure()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "plans", "help-batch.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["batchId"] = "help-batch-007",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Help.Tool",
                        ["version"] = "2.1.0",
                        ["command"] = "help-tool",
                        ["analysisMode"] = "help",
                    },
                },
            });

        var runner = new FakeHelpBatchAnalysisRunner((item, outputRoot, batchId, source, timeouts) =>
        {
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = item.PackageId,
                    ["version"] = item.Version,
                    ["batchId"] = batchId,
                    ["attempt"] = item.Attempt,
                    ["source"] = source,
                    ["analysisMode"] = "help",
                    ["disposition"] = "success",
                    ["command"] = item.CommandName,
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                        ["crawlArtifact"] = "crawl.json",
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "opencli.json"),
                new JsonObject
                {
                    ["opencli"] = "0.1-draft",
                    ["info"] = new JsonObject
                    {
                        ["title"] = "help-tool",
                        ["version"] = "2.1.0",
                    },
                    ["commands"] = new JsonArray(),
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(outputRoot, "crawl.json"),
                new JsonObject
                {
                    ["commands"] = new JsonArray(),
                });
            return 0;
        });

        var service = new HelpBatchAnalysisCommandService(
            runner,
            new FakeCliFxBatchAnalysisRunner((_, _, _, _, _) => throw new InvalidOperationException("CliFx runner should not run.")));
        var exitCode = await service.RunAsync(
            repositoryRoot,
            "plans/help-batch.json",
            "artifacts/help-batch",
            batchId: null,
            source: "help-index-batch",
            targetBranch: "main",
            installTimeoutSeconds: 300,
            analysisTimeoutSeconds: 600,
            commandTimeoutSeconds: 60,
            json: true,
            CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

    private sealed class FakeHelpBatchAnalysisRunner : IHelpBatchAnalysisRunner
    {
        private readonly Func<HelpBatchItem, string, string, string, HelpBatchTimeouts, int> _handler;

        public FakeHelpBatchAnalysisRunner(Func<HelpBatchItem, string, string, string, HelpBatchTimeouts, int> handler)
        {
            _handler = handler;
        }

        public List<FakeInvocation> Invocations { get; } = [];

        public Task<int> RunAsync(
            HelpBatchItem item,
            string outputRoot,
            string batchId,
            string source,
            HelpBatchTimeouts timeouts,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(outputRoot);
            Invocations.Add(new FakeInvocation(outputRoot, batchId, source, timeouts, item.PackageId));
            return Task.FromResult(_handler(item, outputRoot, batchId, source, timeouts));
        }
    }

    private sealed record FakeInvocation(string OutputRoot, string BatchId, string Source, HelpBatchTimeouts Timeouts, string PackageId);

    private sealed class FakeCliFxBatchAnalysisRunner : ICliFxBatchAnalysisRunner
    {
        private readonly Func<HelpBatchItem, string, string, string, HelpBatchTimeouts, int> _handler;

        public FakeCliFxBatchAnalysisRunner(Func<HelpBatchItem, string, string, string, HelpBatchTimeouts, int> handler)
        {
            _handler = handler;
        }

        public List<FakeInvocation> Invocations { get; } = [];

        public Task<int> RunAsync(
            HelpBatchItem item,
            string outputRoot,
            string batchId,
            string source,
            HelpBatchTimeouts timeouts,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(outputRoot);
            Invocations.Add(new FakeInvocation(outputRoot, batchId, source, timeouts, item.PackageId));
            return Task.FromResult(_handler(item, outputRoot, batchId, source, timeouts));
        }
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
