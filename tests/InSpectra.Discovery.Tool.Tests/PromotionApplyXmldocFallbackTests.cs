using System.Text.Json.Nodes;
using Xunit;

[Collection("PromotionApplyCommandService")]
public sealed class PromotionApplyXmldocFallbackTests
{
    [Fact]
    public async Task ApplyUntrustedAsync_Allows_Invalid_OpenCli_When_Xmldoc_Fallback_Is_Available()
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
                    ["generatedAtUtc"] = "2026-03-28T00:00:00Z",
                    ["packageType"] = "DotnetTool",
                    ["packageCount"] = 1,
                    ["packages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Xmldoc.Tool",
                            ["latestVersion"] = "1.2.3",
                            ["totalDownloads"] = 42,
                        },
                    },
                });

            var downloadRoot = Path.Combine(repositoryRoot, "downloads");
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-xmldoc-fallback",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Xmldoc.Tool",
                            ["version"] = "1.2.3",
                            ["attempt"] = 1,
                            ["command"] = "xmldoc-tool",
                            ["analysisMode"] = "native",
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-xmldoc-tool", "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Xmldoc.Tool",
                    ["version"] = "1.2.3",
                    ["batchId"] = "batch-xmldoc-fallback",
                    ["attempt"] = 1,
                    ["source"] = "analyze-untrusted-batch",
                    ["analysisMode"] = "native",
                    ["analyzedAt"] = "2026-03-28T01:00:00Z",
                    ["disposition"] = "success",
                    ["packageUrl"] = "https://www.nuget.org/packages/Xmldoc.Tool/1.2.3",
                    ["packageContentUrl"] = "https://nuget.test/xmldoc.tool.1.2.3.nupkg",
                    ["catalogEntryUrl"] = "https://nuget.test/catalog/xmldoc.tool.1.2.3.json",
                    ["command"] = "xmldoc-tool",
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
                        ["xmldoc"] = new JsonObject
                        {
                            ["status"] = "ok",
                            ["classification"] = "xml-ready",
                        },
                    },
                    ["introspection"] = new JsonObject
                    {
                        ["opencli"] = new JsonObject
                        {
                            ["status"] = "ok",
                            ["classification"] = "json-ready",
                        },
                        ["xmldoc"] = new JsonObject
                        {
                            ["status"] = "ok",
                            ["classification"] = "xml-ready",
                        },
                    },
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                        ["xmldocArtifact"] = "xmldoc.xml",
                    },
                });

            RepositoryPathResolver.WriteTextFile(
                Path.Combine(downloadRoot, "analysis-xmldoc-tool", "opencli.json"),
                "{not valid json");
            RepositoryPathResolver.WriteTextFile(
                Path.Combine(downloadRoot, "analysis-xmldoc-tool", "xmldoc.xml"),
                """
                <Model>
                  <Command Name="__default_command">
                    <Description>Sample XML doc</Description>
                  </Command>
                </Model>
                """);

            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, summaryOutputPath: null, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var metadata = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "xmldoc.tool", "1.2.3", "metadata.json"));
            Assert.Equal("ok", metadata["status"]?.GetValue<string>());
            Assert.Equal("synthesized-from-xmldoc", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
            Assert.Equal("xmldoc", metadata["analysisMode"]?.GetValue<string>());
            Assert.Equal("xmldoc", metadata["analysisSelection"]?["selectedMode"]?.GetValue<string>());
            Assert.Equal("native", metadata["analysisSelection"]?["preferredMode"]?.GetValue<string>());

            var openCli = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "xmldoc.tool", "1.2.3", "opencli.json"));
            Assert.Equal("synthesized-from-xmldoc", openCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
            Assert.Equal("xmldoc-tool", openCli["info"]?["title"]?.GetValue<string>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

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
