using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserThirdPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Recovers_Default_Verbs_And_Preamble_Positionals()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "feval.cli", "1.7.0");
        WriteMetadata(versionRoot, "Feval.Cli", "1.7.0", "feval", rejectedHelpArtifact: true);
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
                            Feval.Cli 1.7.0
                            Copyright (C) 2026 HeChao

                              run        (Default Verb) Running in standalone mode or connect a remote
                                         service

                              alias      Remote feval service address aliases

                              help       Display more information on a specific command.

                              version    Display version information.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "run",
                        ["payload"] =
                            """
                            Feval.Cli 1.7.0
                            Copyright (C) 2026 HeChao

                              --standalone        Running in standalone mode

                              --help              Display this help screen.

                              --version           Display version information.

                              address (pos. 0)    Remote service address formatted like: 127.0.0.1:9999
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "alias",
                        ["payload"] =
                            """
                            Feval.Cli 1.7.0
                            Copyright (C) 2026 HeChao

                              --help              Display this help screen.

                              --version           Display version information.

                              name (pos. 0)       Alias name

                              address (pos. 1)    Alias address
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Contains(openCli["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "run", StringComparison.Ordinal));
        Assert.Contains(openCli["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "alias", StringComparison.Ordinal));

        var run = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "run", StringComparison.Ordinal)));
        Assert.Contains(run!["arguments"]!.AsArray(), argument => string.Equals(argument?["name"]?.GetValue<string>(), "ADDRESS", StringComparison.Ordinal));

        var alias = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "alias", StringComparison.Ordinal)));
        Assert.Contains(alias!["arguments"]!.AsArray(), argument => string.Equals(argument?["name"]?.GetValue<string>(), "NAME", StringComparison.Ordinal));
        Assert.Contains(alias["arguments"]!.AsArray(), argument => string.Equals(argument?["name"]?.GetValue<string>(), "ADDRESS", StringComparison.Ordinal));
    }

    [Fact]
    public void Regenerator_Infers_Blank_Description_Root_Commands()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "ivysoft.gitlab.tools", "1.0.0");
        WriteMetadata(versionRoot, "IVySoft.Gitlab.Tools", "1.0.0", "ivysoft-gitlab-tools", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            IVySoft.Gitlab.Tools 1.0.0
            Copyright Vadim Malyshev 2026

              run-pipeline

              run-manual-job

              wait-merge-request

              help                     Display more information on a specific command.

              version                  Display version information.
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.False(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("partial", metadata["status"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Uses_Markdown_Headings_For_Title_Sections_And_Positionals()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "demo.markdown.cli", "1.0.0");
        WriteMetadata(versionRoot, "demo.markdown.cli", "1.0.0", "demo-md", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            trinsert
            # demo-md

            ## Commands

              build      Build the project

              verify     Verify the project

            ## Usage

            demo-md build <input>

              value pos. 0    Required. Input project path

              --help          Display this help screen.
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("demo-md", openCli["info"]?["title"]?.GetValue<string>());
        Assert.Contains(openCli["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "build", StringComparison.Ordinal));
        Assert.Contains(openCli["arguments"]!.AsArray(), argument => string.Equals(argument?["name"]?.GetValue<string>(), "VALUE", StringComparison.Ordinal));
    }

    [Fact]
    public void Regenerator_Preserves_Dotted_And_Underscored_Option_Names()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.dataapi", "1.0.0");
        WriteMetadata(versionRoot, "sample.dataapi", "1.0.0", "sample-dataapi", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            Sample.DataApi 1.0.0

              -s, --source                Required. Name of the source database object.

              --source.type               Type of the database object.

              --source.params             Source parameters.

              --default_value             Default value to use.

              --help                      Display this help screen.

              --version                   Display version information.
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var optionNames = openCli["options"]!.AsArray()
            .Select(option => option?["name"]?.GetValue<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        Assert.Contains("--source", optionNames);
        Assert.Contains("--source.type", optionNames);
        Assert.Contains("--source.params", optionNames);
        Assert.Contains("--default_value", optionNames);
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
