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
