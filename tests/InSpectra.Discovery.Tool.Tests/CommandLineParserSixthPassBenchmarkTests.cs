namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserSixthPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Preserves_Root_Command_Summaries_And_Infers_Value_Options_From_Descriptions()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var fevalRoot = Path.Combine(repositoryRoot, "index", "packages", "feval.cli", "1.7.0");
        WriteMetadata(fevalRoot, "Feval.Cli", "1.7.0", "feval", rejectedHelpArtifact: true);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(fevalRoot, "crawl.json"),
            new JsonObject
            {
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = null,
                        ["payload"] =
                            """
                            Feval.Cli 1.7.0
                            Copyright (C) 2026 HeChao

                              run        (Default Verb) Running in standalone mode or connect a remote
                                         service

                              using      Set default using namespaces on launch

                              alias      Remote feval service address aliases

                              config     Config feval command line tool

                              help       Display more information on a specific command.

                              version    Display version information.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "config",
                        ["payload"] =
                            """
                            Feval.Cli 1.7.0
                            Copyright (C) 2026 HeChao

                              -l, --list        List all configurations

                              --help            Display this help screen.

                              --version         Display version information.

                              key (pos. 0)      Configuration key

                              value (pos. 1)    Configuration value
                            """,
                    },
                },
            });

        var refreshRoot = Path.Combine(repositoryRoot, "index", "packages", "refresh.tool", "1.3.0");
        WriteMetadata(refreshRoot, "Refresh.Tool", "1.3.0", "refresh", rejectedHelpArtifact: true);
        WriteCrawl(refreshRoot,
            """
            Refresh.Tool 1.3.0
            Copyright (C) 2026 Refresh.Tool

              -p, --Project      Project to be refactored

              -s, --Solution     Solution to be refactored

              -m, --Migration    Required. Path to migration file

              -v, --Verbose      Enable verbose logging

              --help             Display this help screen.

              --version          Display version information.
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(2, result.CandidateCount);
        Assert.Equal(2, result.RewrittenCount);

        var fevalOpenCli = ParseJsonObject(Path.Combine(fevalRoot, "opencli.json"));
        var config = Assert.Single(fevalOpenCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "config", StringComparison.Ordinal)));
        Assert.Equal("Config feval command line tool", config!["description"]?.GetValue<string>());

        var refreshOpenCli = ParseJsonObject(Path.Combine(refreshRoot, "opencli.json"));
        var refreshOptions = refreshOpenCli["options"]!.AsArray();

        var project = Assert.Single(refreshOptions.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--Project", StringComparison.Ordinal)));
        Assert.Equal("PROJECT", project!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.False(project["arguments"]![0]!["required"]!.GetValue<bool>());

        var solution = Assert.Single(refreshOptions.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--Solution", StringComparison.Ordinal)));
        Assert.Equal("SOLUTION", solution!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.False(solution["arguments"]![0]!["required"]!.GetValue<bool>());

        var migration = Assert.Single(refreshOptions.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--Migration", StringComparison.Ordinal)));
        Assert.Equal("MIGRATION", migration!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.True(migration["arguments"]![0]!["required"]!.GetValue<bool>());
        Assert.Equal("Path to migration file", migration["description"]!.GetValue<string>());
    }

    private static void WriteMetadata(string versionRoot, string packageId, string version, string command, bool rejectedHelpArtifact)
    {
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = packageId,
                ["version"] = version,
                ["command"] = command,
                ["cliFramework"] = "CommandLineParser",
                ["status"] = rejectedHelpArtifact ? "partial" : "ok",
                ["analysisMode"] = "help",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = rejectedHelpArtifact ? null : "crawled-from-help",
                        ["classification"] = rejectedHelpArtifact ? "invalid-opencli-artifact" : null,
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliSource"] = rejectedHelpArtifact ? null : "crawled-from-help",
                },
            });
    }

    private static void WriteCrawl(string versionRoot, string payload)
    {
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "crawl.json"),
            new JsonObject
            {
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = null,
                        ["payload"] = payload,
                    },
                },
            });
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON object expected at '{path}'.");

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
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


