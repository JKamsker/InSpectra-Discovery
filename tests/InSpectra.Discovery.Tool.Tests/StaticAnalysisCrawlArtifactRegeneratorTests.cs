namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class StaticAnalysisCrawlArtifactRegeneratorTests
{
    [Fact]
    public void Regenerator_Preserves_Previously_Enriched_NuGet_Description()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["command"] = "sample",
                ["cliFramework"] = "CommandLineParser",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "static-analysis",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    ["crawlPath"] = "index/packages/sample.tool/1.2.3/crawl.json",
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
                            Sample.Tool 1.2.3
                            CLI banner description.

                              --input     Input path.
                              --help      Display this help screen.
                            """,
                    },
                },
                ["staticCommands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["key"] = string.Empty,
                        ["description"] = "CLI banner description.",
                        ["isHidden"] = false,
                        ["values"] = new JsonArray(),
                        ["options"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["longName"] = "input",
                                ["shortName"] = null,
                                ["description"] = "Input path.",
                                ["isRequired"] = false,
                                ["isHidden"] = false,
                                ["isBoolLike"] = false,
                                ["acceptsMultiple"] = false,
                                ["clrType"] = "System.String",
                            },
                        },
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "Sample.Tool",
                    ["version"] = "1.2.3",
                    ["description"] = "NuGet package description.",
                },
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "static-analysis",
                    ["cliParsedDescription"] = "CLI banner description.",
                },
            });

        var regenerator = new StaticAnalysisCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("NuGet package description.", regenerated["info"]?["description"]?.GetValue<string>());
        Assert.Equal("CLI banner description.", regenerated["x-inspectra"]?["cliParsedDescription"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Preserves_Existing_Description_When_Regenerated_Description_Is_Blank()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "2.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "2.0.0",
                ["command"] = "sample",
                ["cliFramework"] = "System.CommandLine",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "static-analysis",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/sample.tool/2.0.0/opencli.json",
                    ["crawlPath"] = "index/packages/sample.tool/2.0.0/crawl.json",
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
                            Description:
                              Sample CLI

                            Usage:
                              sample [options]

                            Options:
                              --help  Show help and usage information
                            """,
                    },
                },
                ["staticCommands"] = new JsonArray(),
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "sample",
                    ["version"] = "2.0.0",
                    ["description"] = "Stored NuGet description.",
                },
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "static-analysis",
                },
            });

        var regenerator = new StaticAnalysisCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("Stored NuGet description.", regenerated["info"]?["description"]?.GetValue<string>());
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"Expected JSON object at '{path}'.");

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"inspectra-tests-{Guid.NewGuid():N}");
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


