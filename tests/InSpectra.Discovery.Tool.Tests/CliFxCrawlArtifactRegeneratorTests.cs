namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.CliFx.Artifacts;
using InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CliFxCrawlArtifactRegeneratorTests
{
    [Fact]
    public void Regenerates_CliFx_OpenCli_From_Stored_Crawl_And_Metadata()
    {
        Runtime.Initialize();

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
        Runtime.Initialize();

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

    [Fact]
    public void Regenerates_TitleCase_CliFx_Crawl_When_OpenCli_Is_Missing()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "trainingmoduleconvertor", "0.0.9");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "TrainingModuleConvertor",
                ["version"] = "0.0.9",
                ["command"] = "training-module-convertor",
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
                    ["metadataPath"] = "index/packages/trainingmoduleconvertor/0.0.9/metadata.json",
                    ["opencliSource"] = "crawled-from-help",
                    ["crawlPath"] = "index/packages/trainingmoduleconvertor/0.0.9/crawl.json",
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
                                Training Modules convertor v0.0.9
                                  Training Modules convertor

                                Usage
                                  dotnet tool.dll [command] [options]

                                Options
                                  -h|--help         Shows help text.

                                Commands
                                  convert           Convert a module.
                                """,
                        },
                    },
                    new JsonObject
                    {
                        ["command"] = "convert",
                        ["result"] = new JsonObject
                        {
                            ["stdout"] =
                                """
                                Description
                                  Convert a module.

                                Usage
                                  dotnet tool.dll convert <folder> [options]

                                Parameters
                                * folder            Root folder.

                                Options
                                  -h|--help         Shows help text.
                                """,
                        },
                    },
                },
            });

        var regenerator = new CliFxCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);
        Assert.True(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("crawled-from-clifx-help", openCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
        Assert.Equal("convert", openCli["commands"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Preserves_CliFramework_From_Existing_OpenCli_When_Metadata_Is_Blank()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "mixedclifx", "1.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "MixedCliFx",
                ["version"] = "1.0.0",
                ["command"] = "mixed",
                ["artifacts"] = new JsonObject
                {
                    ["crawlPath"] = "index/packages/mixedclifx/1.0.0/crawl.json",
                    ["opencliSource"] = "crawled-from-help",
                },
            });
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
                        ["result"] = new JsonObject
                        {
                            ["stdout"] =
                                """
                                mixed 1.0.0

                                USAGE
                                  mixed [options]

                                OPTIONS
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
                    ["cliFramework"] = "CliFx + System.CommandLine",
                },
                ["commands"] = new JsonArray(),
            });

        var regenerator = new CliFxCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal("CliFx + System.CommandLine", ParseJsonObject(Path.Combine(versionRoot, "opencli.json"))["x-inspectra"]?["cliFramework"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Drops_Unreachable_CliFx_Captures_And_Respects_Metadata_OpenCli_Path()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sampleclifx", "3.0.0");
        var redirectedOpenCliPath = Path.Combine(repositoryRoot, "index", "packages", "sampleclifx", "3.0.0", "replayed", "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "SampleCliFx",
                ["version"] = "3.0.0",
                ["command"] = "sample",
                ["cliFramework"] = "CliFx",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "crawled-from-clifx-help",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sampleclifx/3.0.0/metadata.json",
                    ["opencliPath"] = "index/packages/sampleclifx/3.0.0/replayed/opencli.json",
                    ["opencliSource"] = "crawled-from-clifx-help",
                    ["crawlPath"] = "index/packages/sampleclifx/3.0.0/crawl.json",
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
                            sample 3.0.0

                            USAGE
                              sample [command]

                            COMMANDS
                              upload            Upload a package
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "upload",
                        ["payload"] =
                            """
                            sample 3.0.0

                            USAGE
                              sample upload [options]

                            DESCRIPTION
                              Upload a package

                            OPTIONS
                              --file <path>     File to upload
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "orphan",
                        ["payload"] =
                            """
                            sample 3.0.0

                            USAGE
                              sample orphan [options]

                            DESCRIPTION
                              Should not survive replay
                            """,
                    },
                },
                ["staticCommands"] = CliFxCrawlArtifactSupport.SerializeStaticCommands(
                    new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["upload"] = new(
                            Name: "upload",
                            Description: "Upload a package",
                            Parameters: [],
                            Options: []),
                    }),
            });
        RepositoryPathResolver.WriteJsonFile(
            redirectedOpenCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-clifx-help",
                    ["cliFramework"] = "CliFx",
                },
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "stale",
                        ["hidden"] = false,
                    },
                },
            });

        var regenerator = new CliFxCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.False(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var regenerated = ParseJsonObject(redirectedOpenCliPath);
        var commandNode = Assert.Single(regenerated["commands"]!.AsArray());
        Assert.NotNull(commandNode);
        var command = commandNode!.AsObject();
        Assert.Equal("upload", command["name"]!.GetValue<string>());
        Assert.DoesNotContain(regenerated["commands"]!.AsArray(), candidate =>
            string.Equals(candidate?["name"]?.GetValue<string>(), "orphan", StringComparison.Ordinal));
    }

    [Fact]
    public void Regenerator_Respects_Metadata_Crawl_Path()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sampleclifx", "4.0.0");
        var redirectedCrawlPath = Path.Combine(repositoryRoot, "index", "packages", "sampleclifx", "4.0.0", "replayed", "crawl.json");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "SampleCliFx",
                ["version"] = "4.0.0",
                ["command"] = "sample",
                ["cliFramework"] = "CliFx",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "crawled-from-clifx-help",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sampleclifx/4.0.0/metadata.json",
                    ["opencliPath"] = "index/packages/sampleclifx/4.0.0/opencli.json",
                    ["opencliSource"] = "crawled-from-clifx-help",
                    ["crawlPath"] = "index/packages/sampleclifx/4.0.0/replayed/crawl.json",
                },
            });

        RepositoryPathResolver.WriteJsonFile(
            redirectedCrawlPath,
            new JsonObject
            {
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = null,
                        ["payload"] =
                            """
                            sample 4.0.0

                            USAGE
                              sample [command]

                            COMMANDS
                              upload            Upload a package
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "upload",
                        ["payload"] =
                            """
                            sample 4.0.0

                            USAGE
                              sample upload [options]

                            DESCRIPTION
                              Upload a package
                            """,
                    },
                },
                ["staticCommands"] = CliFxCrawlArtifactSupport.SerializeStaticCommands(
                    new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["upload"] = new(
                            Name: "upload",
                            Description: "Upload a package",
                            Parameters: [],
                            Options: []),
                    }),
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-clifx-help",
                    ["cliFramework"] = "CliFx",
                },
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "stale",
                        ["hidden"] = false,
                    },
                },
            });

        var regenerator = new CliFxCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var command = Assert.IsType<JsonObject>(Assert.Single(regenerated["commands"]!.AsArray()));
        Assert.Equal("upload", command["name"]?.GetValue<string>());
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


