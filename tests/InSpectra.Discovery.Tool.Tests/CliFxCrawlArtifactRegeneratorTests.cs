using System.Text.Json.Nodes;
using Xunit;

public sealed class CliFxCrawlArtifactRegeneratorTests
{
    [Fact]
    public void Regenerates_CliFx_OpenCli_From_Stored_Crawl_And_Metadata()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sampleclifx", "1.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "SampleCliFx",
                ["version"] = "1.0.0",
                ["command"] = "sample",
                ["cliFramework"] = "CliFx",
            });

        var staticCommands = new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Name: null,
                Description: "Default command",
                Parameters: [],
                Options: []),
            ["user add"] = new(
                Name: "user add",
                Description: "Adds a user",
                Parameters: [],
                Options:
                [
                    new CliFxOptionDefinition(
                        Name: null,
                        ShortName: 's',
                        IsRequired: true,
                        IsSequence: false,
                        IsBoolLike: false,
                        ClrType: "System.String",
                        Description: "Script path",
                        EnvironmentVariable: null,
                        AcceptedValues: [],
                        ValueName: "scriptPath"),
                ]),
        };

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "crawl.json"),
            new JsonObject
            {
                ["documentCount"] = 1,
                ["captureCount"] = 1,
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = null,
                        ["payload"] =
                            """
                            sample 1.0.0

                            DESCRIPTION
                              Demo CLI
                            """,
                    },
                },
                ["staticCommands"] = CliFxCrawlArtifactSupport.SerializeStaticCommands(staticCommands),
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-clifx-help",
                },
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "user add",
                        ["hidden"] = false,
                    },
                },
            });

        var regenerator = new CliFxCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("crawled-from-clifx-help", regenerated["x-inspectra"]?["artifactSource"]?.GetValue<string>());
        Assert.Equal("CliFx", regenerated["x-inspectra"]?["cliFramework"]?.GetValue<string>());

        var userNode = Assert.Single(regenerated["commands"]!.AsArray());
        Assert.NotNull(userNode);
        var user = userNode!.AsObject();
        Assert.Equal("user", user["name"]!.GetValue<string>());
        var addNode = Assert.Single(user["commands"]!.AsArray());
        Assert.NotNull(addNode);
        var add = addNode!.AsObject();
        var optionNode = Assert.Single(add["options"]!.AsArray());
        Assert.NotNull(optionNode);
        var option = optionNode!.AsObject();
        var argumentNode = Assert.Single(option["arguments"]!.AsArray());
        Assert.NotNull(argumentNode);
        var argument = argumentNode!.AsObject();

        Assert.Equal("add", add["name"]!.GetValue<string>());
        Assert.Equal("-s", option["name"]!.GetValue<string>());
        Assert.Equal("SCRIPT_PATH", argument["name"]!.GetValue<string>());
    }

    [Fact]
    public void Regenerates_Legacy_CliFx_Crawl_From_Result_Payload_And_Historic_Artifact_Source()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "legacyclifx", "2.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "LegacyCliFx",
                ["version"] = "2.0.0",
                ["command"] = "legacy",
                ["cliFramework"] = "CliFx",
                ["status"] = "partial",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "crawled-from-help",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/legacyclifx/2.0.0/metadata.json",
                    ["opencliPath"] = "index/packages/legacyclifx/2.0.0/opencli.json",
                    ["opencliSource"] = "crawled-from-help",
                    ["crawlPath"] = "index/packages/legacyclifx/2.0.0/crawl.json",
                },
            });

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "crawl.json"),
            new JsonObject
            {
                ["documentCount"] = 2,
                ["captureCount"] = 2,
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = null,
                        ["result"] = new JsonObject
                        {
                            ["stdout"] =
                                """
                                legacy 2.0.0

                                USAGE
                                  legacy [command] [...]

                                COMMANDS
                                  upload            Upload a package
                                """,
                        },
                    },
                    new JsonObject
                    {
                        ["command"] = "upload",
                        ["result"] = new JsonObject
                        {
                            ["stdout"] =
                                """
                                legacy 2.0.0

                                USAGE
                                  legacy upload --pat <token> [--folder <path>] [options]

                                DESCRIPTION
                                  Upload a package

                                OPTIONS
                                * -p|--pat          Personal access token
                                  -f|--folder       Folder to upload
                                  -h|--help         Shows help text.
                                """,
                        },
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-help",
                    ["cliFramework"] = "CliFx",
                },
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "upload",
                        ["hidden"] = false,
                    },
                },
            });

        var regenerator = new CliFxCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("crawled-from-clifx-help", regenerated["x-inspectra"]?["artifactSource"]?.GetValue<string>());

        var commandNode = Assert.Single(regenerated["commands"]!.AsArray());
        Assert.NotNull(commandNode);
        var command = commandNode!.AsObject();
        Assert.Equal("upload", command["name"]!.GetValue<string>());
        var patOptionNode = command["options"]!.AsArray()[0];
        Assert.NotNull(patOptionNode);
        var patOption = patOptionNode!.AsObject();
        Assert.Equal("--pat", patOption["name"]!.GetValue<string>());
        Assert.Equal("TOKEN", patOption["arguments"]![0]!["name"]!.GetValue<string>());

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("crawled-from-clifx-help", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "legacyclifx", "latest", "opencli.json")));
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
