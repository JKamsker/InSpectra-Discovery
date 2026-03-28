using System.Text.Json.Nodes;
using Xunit;

public sealed class PromotionApplyCommandServiceTests
{
    [Fact]
    public async Task ApplyUntrustedAsync_ProjectsTotalDownloadsIntoMetadataAndIndexes()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var previousRepositoryRoot = Environment.GetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT");
        Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", repositoryRoot);

        try
        {
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json"),
                new JsonObject
                {
                    ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                    ["packageType"] = "DotnetTool",
                    ["packageCount"] = 2,
                    ["source"] = new JsonObject
                    {
                        ["serviceIndexUrl"] = "https://api.nuget.org/v3/index.json",
                        ["autocompleteUrl"] = "https://nuget.test/autocomplete",
                        ["searchUrl"] = "https://nuget.test/search",
                        ["registrationBaseUrl"] = "https://nuget.test/registration/",
                        ["prefixAlphabet"] = "abc",
                        ["expectedPackageCount"] = 2,
                        ["sortOrder"] = "totalDownloads-desc",
                    },
                    ["packages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Existing.Tool",
                            ["latestVersion"] = "1.0.0",
                            ["totalDownloads"] = 4321,
                            ["projectUrl"] = "https://github.com/example/existing.tool",
                        },
                        new JsonObject
                        {
                            ["packageId"] = "Sample.Tool",
                            ["latestVersion"] = "1.2.3",
                            ["totalDownloads"] = 1234,
                            ["projectUrl"] = "https://sample.tool.example",
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(repositoryRoot, "index", "packages", "existing.tool", "1.0.0", "metadata.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Existing.Tool",
                    ["version"] = "1.0.0",
                    ["trusted"] = false,
                    ["source"] = "seed",
                    ["batchId"] = "seed",
                    ["attempt"] = 1,
                    ["status"] = "partial",
                    ["evaluatedAt"] = "2026-03-20T00:00:00Z",
                    ["publishedAt"] = "2026-03-19T00:00:00Z",
                    ["packageUrl"] = "https://www.nuget.org/packages/Existing.Tool/1.0.0",
                    ["packageContentUrl"] = "https://nuget.test/existing.tool.1.0.0.nupkg",
                    ["registrationLeafUrl"] = "https://nuget.test/registration/existing.tool/1.0.0.json",
                    ["catalogEntryUrl"] = "https://nuget.test/catalog/existing.tool.1.0.0.json",
                    ["command"] = "existing",
                    ["entryPoint"] = "existing.dll",
                    ["runner"] = "dotnet",
                    ["toolSettingsPath"] = "tools/net10.0/any/DotnetToolSettings.xml",
                    ["detection"] = new JsonObject(),
                    ["introspection"] = new JsonObject
                    {
                        ["opencli"] = null,
                        ["xmldoc"] = null,
                    },
                    ["timings"] = new JsonObject
                    {
                        ["totalMs"] = 100,
                    },
                    ["steps"] = new JsonObject
                    {
                        ["install"] = null,
                        ["opencli"] = null,
                        ["xmldoc"] = null,
                    },
                    ["artifacts"] = new JsonObject
                    {
                        ["metadataPath"] = "index/packages/existing.tool/1.0.0/metadata.json",
                        ["opencliPath"] = null,
                        ["opencliSource"] = null,
                        ["xmldocPath"] = null,
                    },
                });

            var downloadRoot = Path.Combine(repositoryRoot, "downloads");
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-001",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Sample.Tool",
                            ["version"] = "1.2.3",
                            ["attempt"] = 1,
                            ["cliFramework"] = "System.CommandLine",
                            ["packageUrl"] = "https://www.nuget.org/packages/Sample.Tool/1.2.3",
                            ["packageContentUrl"] = "https://nuget.test/sample.tool.1.2.3.nupkg",
                            ["catalogEntryUrl"] = "https://nuget.test/catalog/sample.tool.1.2.3.json",
                            ["totalDownloads"] = 1234,
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-sample-tool", "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Sample.Tool",
                    ["version"] = "1.2.3",
                    ["batchId"] = "batch-001",
                    ["attempt"] = 1,
                    ["trusted"] = false,
                    ["source"] = "analyze-untrusted-batch",
                    ["cliFramework"] = "System.CommandLine",
                    ["analyzedAt"] = "2026-03-27T01:00:00Z",
                    ["disposition"] = "success",
                    ["retryEligible"] = false,
                    ["phase"] = "complete",
                    ["classification"] = "complete",
                    ["failureMessage"] = null,
                    ["failureSignature"] = null,
                    ["packageUrl"] = "https://www.nuget.org/packages/Sample.Tool/1.2.3",
                    ["packageContentUrl"] = "https://nuget.test/sample.tool.1.2.3.nupkg",
                    ["registrationLeafUrl"] = "https://nuget.test/registration/sample.tool/1.2.3.json",
                    ["catalogEntryUrl"] = "https://nuget.test/catalog/sample.tool.1.2.3.json",
                    ["projectUrl"] = "https://sample.tool.example",
                    ["sourceRepositoryUrl"] = "https://github.com/example/sample.tool.git",
                    ["publishedAt"] = "2026-03-27T00:30:00Z",
                    ["command"] = "sample",
                    ["entryPoint"] = "sample.dll",
                    ["runner"] = "dotnet",
                    ["toolSettingsPath"] = "tools/net10.0/any/DotnetToolSettings.xml",
                    ["detection"] = new JsonObject
                    {
                        ["hasSpectreConsole"] = true,
                        ["hasSpectreConsoleCli"] = true,
                        ["matchedPackageEntries"] = new JsonArray(),
                        ["matchedDependencyIds"] = new JsonArray(),
                    },
                    ["introspection"] = new JsonObject
                    {
                        ["opencli"] = new JsonObject
                        {
                            ["status"] = "ok",
                            ["classification"] = "json-ready",
                        },
                        ["xmldoc"] = null,
                    },
                    ["timings"] = new JsonObject
                    {
                        ["totalMs"] = 500,
                        ["installMs"] = 100,
                        ["opencliMs"] = 200,
                        ["xmldocMs"] = null,
                    },
                    ["steps"] = new JsonObject
                    {
                        ["install"] = new JsonObject
                        {
                            ["status"] = "ok",
                        },
                        ["opencli"] = new JsonObject
                        {
                            ["status"] = "ok",
                            ["classification"] = "json-ready",
                        },
                        ["xmldoc"] = null,
                    },
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                        ["crawlArtifact"] = "crawl.json",
                        ["xmldocArtifact"] = null,
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-sample-tool", "opencli.json"),
                new JsonObject
                {
                    ["opencli"] = "0.1-draft",
                    ["info"] = new JsonObject
                    {
                        ["title"] = "sample",
                        ["version"] = "1.0",
                        ["description"] = null,
                    },
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "--verbose",
                            ["required"] = false,
                            ["description"] = null,
                            ["aliases"] = new JsonArray(),
                        },
                    },
                    ["commands"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "serve",
                            ["description"] = null,
                            ["arguments"] = null,
                            ["examples"] = new JsonArray(),
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-sample-tool", "crawl.json"),
                new JsonObject
                {
                    ["documentCount"] = 1,
                    ["captureCount"] = 1,
                    ["commands"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["command"] = "sample",
                            ["parsed"] = true,
                        },
                    },
                });

            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, summaryOutputPath: null, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var sampleMetadata = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3", "metadata.json"));
            Assert.Equal(1234L, sampleMetadata["totalDownloads"]?.GetValue<long>());
            Assert.Equal("System.CommandLine", sampleMetadata["cliFramework"]?.GetValue<string>());
            Assert.Equal("index/packages/sample.tool/1.2.3/crawl.json", sampleMetadata["artifacts"]?["crawlPath"]?.GetValue<string>());
            Assert.Equal("tool-output", sampleMetadata["artifacts"]?["opencliSource"]?.GetValue<string>());

            var sampleCrawl = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3", "crawl.json"));
            Assert.Equal(1, sampleCrawl["captureCount"]?.GetValue<int>());
            Assert.Equal("sample", sampleCrawl["commands"]?[0]?["command"]?.GetValue<string>());

            var sampleOpenCli = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3", "opencli.json"));
            Assert.Equal("tool-output", sampleOpenCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
            Assert.False(sampleOpenCli["info"]!.AsObject().ContainsKey("description"));

            var sampleOption = sampleOpenCli["options"]![0]!.AsObject();
            Assert.False(sampleOption.ContainsKey("required"));
            Assert.False(sampleOption.ContainsKey("description"));
            Assert.False(sampleOption.ContainsKey("aliases"));

            var sampleCommand = sampleOpenCli["commands"]![0]!.AsObject();
            Assert.False(sampleCommand.ContainsKey("description"));
            Assert.False(sampleCommand.ContainsKey("arguments"));
            Assert.False(sampleCommand.ContainsKey("examples"));

            var existingPackageIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "existing.tool", "index.json"));
            Assert.Equal(4321L, existingPackageIndex["totalDownloads"]?.GetValue<long>());
            Assert.Equal("https://www.nuget.org/packages/Existing.Tool", existingPackageIndex["links"]?["nuget"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/existing.tool", existingPackageIndex["links"]?["project"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/existing.tool", existingPackageIndex["links"]?["source"]?.GetValue<string>());

            var samplePackageIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "index.json"));
            Assert.Equal(1234L, samplePackageIndex["totalDownloads"]?.GetValue<long>());
            Assert.Equal("System.CommandLine", samplePackageIndex["cliFramework"]?.GetValue<string>());
            Assert.Equal("https://www.nuget.org/packages/Sample.Tool", samplePackageIndex["links"]?["nuget"]?.GetValue<string>());
            Assert.Equal("https://sample.tool.example", samplePackageIndex["links"]?["project"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/sample.tool", samplePackageIndex["links"]?["source"]?.GetValue<string>());

            var allIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "all.json"));
            Assert.Equal(4321L, FindPackage(allIndex, "Existing.Tool")["totalDownloads"]?.GetValue<long>());
            Assert.Equal(1234L, FindPackage(allIndex, "Sample.Tool")["totalDownloads"]?.GetValue<long>());
            Assert.Equal("System.CommandLine", FindPackage(allIndex, "Sample.Tool")["cliFramework"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/existing.tool", FindPackage(allIndex, "Existing.Tool")["links"]?["source"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/sample.tool", FindPackage(allIndex, "Sample.Tool")["links"]?["source"]?.GetValue<string>());

            var browserIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "index.json"));
            Assert.Equal(4321L, FindPackage(browserIndex, "Existing.Tool")["totalDownloads"]?.GetValue<long>());
            Assert.Equal(1234L, FindPackage(browserIndex, "Sample.Tool")["totalDownloads"]?.GetValue<long>());
            Assert.Equal("System.CommandLine", FindPackage(browserIndex, "Sample.Tool")["cliFramework"]?.GetValue<string>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    [Fact]
    public async Task ApplyUntrustedAsync_MergesMultipleExpectedPlans()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var previousRepositoryRoot = Environment.GetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT");
        Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", repositoryRoot);

        try
        {
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json"),
                new JsonObject
                {
                    ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                    ["packageType"] = "DotnetTool",
                    ["packageCount"] = 2,
                    ["source"] = new JsonObject
                    {
                        ["serviceIndexUrl"] = "https://api.nuget.org/v3/index.json",
                        ["sortOrder"] = "totalDownloads-desc",
                    },
                    ["packages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Alpha.Tool",
                            ["latestVersion"] = "1.0.0",
                            ["totalDownloads"] = 300,
                            ["projectUrl"] = "https://alpha.example",
                        },
                        new JsonObject
                        {
                            ["packageId"] = "Beta.Tool",
                            ["latestVersion"] = "2.0.0",
                            ["totalDownloads"] = 200,
                            ["projectUrl"] = "https://beta.example",
                        },
                    },
                });

            var downloadRoot = Path.Combine(repositoryRoot, "downloads");
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan-a", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-a",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Alpha.Tool",
                            ["version"] = "1.0.0",
                            ["attempt"] = 1,
                            ["totalDownloads"] = 300,
                        },
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan-b", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-b",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Beta.Tool",
                            ["version"] = "2.0.0",
                            ["attempt"] = 1,
                            ["totalDownloads"] = 200,
                        },
                    },
                });

            WriteSuccessAnalysis(downloadRoot, "Alpha.Tool", "1.0.0", "alpha", 300);
            WriteSuccessAnalysis(downloadRoot, "Beta.Tool", "2.0.0", "beta", 200);

            var summaryPath = Path.Combine(repositoryRoot, "promotion-summary.json");
            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, summaryPath, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var summary = ParseJsonObject(summaryPath);
            Assert.Equal("aggregate-2-plans", summary["batchId"]?.GetValue<string>());
            Assert.Equal(2, summary["expectedCount"]?.GetValue<int>());
            Assert.Equal(2, summary["successCount"]?.GetValue<int>());

            Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "alpha.tool", "1.0.0", "metadata.json")));
            Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "beta.tool", "2.0.0", "metadata.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

    private static JsonObject FindPackage(JsonObject manifest, string packageId)
        => manifest["packages"]?.AsArray().OfType<JsonObject>()
               .Single(package => string.Equals(package["packageId"]?.GetValue<string>(), packageId, StringComparison.Ordinal))
           ?? throw new InvalidOperationException($"Package '{packageId}' was not found.");

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

    private static void WriteSuccessAnalysis(string downloadRoot, string packageId, string version, string command, long totalDownloads)
    {
        var artifactDirectory = Path.Combine(downloadRoot, $"analysis-{packageId.ToLowerInvariant()}");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(artifactDirectory, "result.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = packageId,
                ["version"] = version,
                ["batchId"] = "batch",
                ["attempt"] = 1,
                ["trusted"] = false,
                ["source"] = "analyze-untrusted-batch",
                ["cliFramework"] = "System.CommandLine",
                ["analyzedAt"] = "2026-03-27T01:00:00Z",
                ["disposition"] = "success",
                ["packageUrl"] = $"https://www.nuget.org/packages/{packageId}/{version}",
                ["packageContentUrl"] = $"https://nuget.test/{packageId.ToLowerInvariant()}.{version}.nupkg",
                ["registrationLeafUrl"] = $"https://nuget.test/registration/{packageId.ToLowerInvariant()}/{version}.json",
                ["catalogEntryUrl"] = $"https://nuget.test/catalog/{packageId.ToLowerInvariant()}.{version}.json",
                ["projectUrl"] = $"https://{packageId.ToLowerInvariant()}.example",
                ["publishedAt"] = "2026-03-27T00:30:00Z",
                ["totalDownloads"] = totalDownloads,
                ["command"] = command,
                ["steps"] = new JsonObject
                {
                    ["install"] = new JsonObject
                    {
                        ["status"] = "ok",
                    },
                    ["opencli"] = null,
                    ["xmldoc"] = null,
                },
                ["timings"] = new JsonObject
                {
                    ["totalMs"] = 100,
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliArtifact"] = "opencli.json",
                    ["xmldocArtifact"] = null,
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(artifactDirectory, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = command,
                    ["version"] = "1.0",
                },
                ["commands"] = new JsonArray(),
            });
    }
}
