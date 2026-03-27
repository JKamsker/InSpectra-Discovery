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
                        },
                        new JsonObject
                        {
                            ["packageId"] = "Sample.Tool",
                            ["latestVersion"] = "1.2.3",
                            ["totalDownloads"] = 1234,
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
                    },
                    ["commands"] = new JsonArray(),
                });

            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, summaryOutputPath: null, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var sampleMetadata = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3", "metadata.json"));
            Assert.Equal(1234L, sampleMetadata["totalDownloads"]?.GetValue<long>());

            var existingPackageIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "existing.tool", "index.json"));
            Assert.Equal(4321L, existingPackageIndex["totalDownloads"]?.GetValue<long>());

            var samplePackageIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "index.json"));
            Assert.Equal(1234L, samplePackageIndex["totalDownloads"]?.GetValue<long>());

            var allIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "all.json"));
            Assert.Equal(4321L, FindPackage(allIndex, "Existing.Tool")["totalDownloads"]?.GetValue<long>());
            Assert.Equal(1234L, FindPackage(allIndex, "Sample.Tool")["totalDownloads"]?.GetValue<long>());

            var browserIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "index.json"));
            Assert.Equal(4321L, FindPackage(browserIndex, "Existing.Tool")["totalDownloads"]?.GetValue<long>());
            Assert.Equal(1234L, FindPackage(browserIndex, "Sample.Tool")["totalDownloads"]?.GetValue<long>());
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
}
