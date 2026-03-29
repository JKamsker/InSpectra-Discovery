using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserFifthPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Traverses_Nested_CommandLineParser_Dispatchers()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "peer", "1.10.0");
        WriteMetadata(versionRoot, "peer", "1.10.0", "peer", rejectedHelpArtifact: true);
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
                            peer 1.10.0
                            Copyright (C) 2026 wareismymind

                              config     Create or edit the configuration for peer

                              show       (Default Verb) Display pull requests assigned to your account

                              help       Display more information on a specific command.

                              version    Display version information.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "config",
                        ["payload"] =
                            """
                            peer 1.10.0
                            Copyright (C) 2026 wareismymind

                            ERROR(S):
                              No verb selected.

                              init       Create a default configuration file

                              show       Print the current config and it's location

                              edit       Open your config in your default text editor

                              help       Display more information on a specific command.

                              version    Display version information.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "config init",
                        ["payload"] =
                            """
                            peer 1.10.0
                            Copyright (C) 2026 wareismymind

                              -f, --force    Overwrite any existing configuration

                              --help         Display this help screen.

                              --version      Display version information.
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var config = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "config", StringComparison.Ordinal)));
        Assert.Contains(config!["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "init", StringComparison.Ordinal));
        Assert.Contains(config["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "show", StringComparison.Ordinal));
        Assert.Contains(config["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "edit", StringComparison.Ordinal));

        var init = Assert.Single(config["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "init", StringComparison.Ordinal)));
        Assert.Contains(init!["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--force", StringComparison.Ordinal));

        var help = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "help", StringComparison.Ordinal)));
        Assert.Null(help!["commands"]);

        var version = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "version", StringComparison.Ordinal)));
        Assert.Null(version!["commands"]);
    }

    [Fact]
    public void Regenerator_Prefers_Command_Specific_CommandLineParser_Payload_Over_Repeated_Root_Inventory()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "docfx2xml", "1.0.4");
        WriteMetadata(versionRoot, "Docfx2xml", "1.0.4", "docfx2xml", rejectedHelpArtifact: true);
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
                            HELP:

                              init       (Default Verb) initialize new convertConfig.json file if not exist
                              run_j      Run convert docfx yaml to xml, using json config file(convertConfig.json)
                              run_x      Run convert docfx yaml to xml, using xml config file(ConvertConfig.xml)
                              run_a      Run convert docfx yaml to xml, using cmd args as config values
                              help       Display more information on a specific command.
                              version    Display version information.

                            Docfx2xml 1.0.4
                            Opensquiggly.com
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "init",
                        ["payload"] =
                            """
                            HELP:

                              init       (Default Verb) initialize new convertConfig.json file if not exist
                              run_j      Run convert docfx yaml to xml, using json config file(convertConfig.json)
                              run_x      Run convert docfx yaml to xml, using xml config file(ConvertConfig.xml)
                              run_a      Run convert docfx yaml to xml, using cmd args as config values
                              help       Display more information on a specific command.
                              version    Display version information.
                            Docfx2xml 1.0.4
                            Opensquiggly.com

                              -f, --file    (Default: convertConfig.json) Config json file name

                              --help        Display this help screen.

                              --version     Display version information.
                            """,
                        ["result"] = new JsonObject
                        {
                            ["stdout"] =
                                """
                                HELP:

                                  init       (Default Verb) initialize new convertConfig.json file if not exist
                                  run_j      Run convert docfx yaml to xml, using json config file(convertConfig.json)
                                  run_x      Run convert docfx yaml to xml, using xml config file(ConvertConfig.xml)
                                  run_a      Run convert docfx yaml to xml, using cmd args as config values
                                  help       Display more information on a specific command.
                                  version    Display version information.
                                """,
                            ["stderr"] =
                                """
                                Docfx2xml 1.0.4
                                Opensquiggly.com

                                  -f, --file    (Default: convertConfig.json) Config json file name

                                  --help        Display this help screen.

                                  --version     Display version information.
                                """,
                        },
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var init = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "init", StringComparison.Ordinal)));
        Assert.Contains(init!["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--file", StringComparison.Ordinal));
        Assert.Null(init["commands"]);

        var version = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "version", StringComparison.Ordinal)));
        Assert.Null(version!["commands"]);
    }

    [Fact]
    public void Regenerator_Recovers_Blank_CommandLineParser_Options_From_Markdown_Footers()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "trsponge", "0.23.43");
        WriteMetadata(versionRoot, "trsponge", "0.23.43", "trsponge", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            trsponge
            Copyright (c) 2023 Ken Domino
            # trsponge

            ## Summary

            Extract parsing results output of Trash command into files

            ## Description

            Read the parse tree data from stdin and write the
            results to file(s).

            ## Usage

                trsponge <options>

            ## Example

                trparse Arithmetic.g4 | trsplit | trsponge

            ## Current version

            0.23.43 Fixes to trgen templates.

            ## License

            The MIT License

              -f, --file                Read parse tree data from file instead of stdin.
              -c, --clobber             (Default: false)
              -o, --output-directory
              -v, --verbose
              --version
              --help                    Display this help screen.
              --version                 Display version information.
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
        Assert.Contains("--output-directory", optionNames);
        Assert.Contains("--verbose", optionNames);
        Assert.Equal(1, optionNames.Count(name => string.Equals(name, "--version", StringComparison.Ordinal)));
    }

    [Fact]
    public void Regenerator_Recovers_CommandLineParser_Preamble_Positionals_After_Blank_Option_Rows()
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

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var config = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "config", StringComparison.Ordinal)));
        Assert.Contains(config!["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--list", StringComparison.Ordinal));
        Assert.Contains(config["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--help", StringComparison.Ordinal));
        Assert.Contains(config["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--version", StringComparison.Ordinal));
        Assert.Contains(config["arguments"]!.AsArray(), argument => string.Equals(argument?["name"]?.GetValue<string>(), "KEY", StringComparison.Ordinal));
        Assert.Contains(config["arguments"]!.AsArray(), argument => string.Equals(argument?["name"]?.GetValue<string>(), "VALUE", StringComparison.Ordinal));
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
