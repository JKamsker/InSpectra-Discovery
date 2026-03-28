using System.Text.Json.Nodes;
using Xunit;

public sealed class ToolHelpCrawlCanonicalizationTests
{
    [Fact]
    public void Regenerator_Canonicalizes_Alias_Bearing_Stored_Command_Keys()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "cicee", "2.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "cicee",
                ["version"] = "2.0.0",
                ["command"] = "cicee",
                ["artifacts"] = new JsonObject
                {
                    ["opencliSource"] = "crawled-from-help",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "crawl.json"),
            new JsonObject
            {
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = null,
                        ["payload"] =
                            """
                            cicee 2.0.0

                            Commands:
                              meta  Metadata operations
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "meta",
                        ["payload"] =
                            """
                            Usage: cicee meta [command]

                            Commands:
                              cienv, cienvironment  Manage CI environment metadata.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "meta cienv, cienvironment",
                        ["payload"] =
                            """
                            Usage: cicee meta cienvironment [options]

                            Options:
                              --verbose  Verbose output.
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var rootCommands = Assert.IsType<JsonArray>(openCli["commands"]);
        var meta = Assert.IsType<JsonObject>(Assert.Single(rootCommands));
        Assert.Equal("meta", meta["name"]!.GetValue<string>());

        var metaCommands = Assert.IsType<JsonArray>(meta["commands"]);
        var cienvironment = Assert.IsType<JsonObject>(Assert.Single(metaCommands));
        Assert.Equal("cienvironment", cienvironment["name"]!.GetValue<string>());
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
