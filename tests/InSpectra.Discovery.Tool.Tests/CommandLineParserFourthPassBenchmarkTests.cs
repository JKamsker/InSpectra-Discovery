using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserFourthPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Parses_CommandLineParser_Alias_Command_Inventory_As_Commands()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "equadrat.licensing.cli", "0.1.3");
        WriteMetadata(versionRoot, "equadrat.Licensing.CLI", "0.1.3", "equadrat.licensing", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            equadrat Licensing 0.1.3
            Copyright © equadrat 2004 - 2024

              k, keypair     Generates a new key-pair.

              t, template    Generates a new template.

              s, sign        Signs a license.

              v, validate    Validates a license.

              help           Display more information on a specific command.

              version        Display version information.
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.DoesNotContain(openCli["options"]?.AsArray() ?? new JsonArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--keypair", StringComparison.Ordinal));
        Assert.Contains(openCli["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "keypair", StringComparison.Ordinal));
        Assert.Contains(openCli["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "template", StringComparison.Ordinal));
        Assert.Contains(openCli["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "sign", StringComparison.Ordinal));
        Assert.Contains(openCli["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "validate", StringComparison.Ordinal));
    }

    [Fact]
    public void Regenerator_Does_Not_Let_CommandLineParser_Positionals_Bleed_Into_Version_Description()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "dbup-cli", "1.8.1");
        WriteMetadata(versionRoot, "dbup-cli", "1.8.1", "dbup-cli", rejectedHelpArtifact: true);
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
                            dbup-cli 1.8.1
                            Copyright (c) 2023 Sergey Tregub

                              drop    Drop database if exists
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "drop",
                        ["payload"] =
                            """
                            dbup-cli 1.8.1
                            Copyright (c) 2023 Sergey Tregub

                              -e, --env      Path to an environment file. Can be more than one file
                                             specified. The path can be absolute or relative against a
                                             current directory

                              -v, --verbosity    (Default: Normal) Verbosity level. Can be one of:
                                                 detail, normal or min

                              --help         Display this help screen.

                              --version      Display version information.

                              value (pos. 0) Path to a configuration file. The path can be absolute or
                                             relative against a current directory
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var drop = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "drop", StringComparison.Ordinal)));
        var versionOption = Assert.Single(drop!["options"]!.AsArray().Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--version", StringComparison.Ordinal)));
        Assert.Equal("Display version information.", versionOption!["description"]?.GetValue<string>());
        Assert.Contains(drop["arguments"]!.AsArray(), argument => string.Equals(argument?["name"]?.GetValue<string>(), "VALUE", StringComparison.Ordinal));
    }

    [Fact]
    public void Regenerator_Ignores_Example_Command_Headings_And_License_Prose()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "trtokens", "0.23.43");
        WriteMetadata(versionRoot, "trtokens", "0.23.43", "trtokens", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            trtokens
            Copyright (c) 2023 Ken Domino
            # trtokens

            ## Summary

            Print tokens in a parse tree

            ## Description

            The trtokens command reads standard in for a parsing result set and prints out
            the tokens for each result.

            ## Examples

            Command:

                trparse -i "1 * 2 + 3" | trquery "grep //expression" | trtokens

            ## License

            The MIT License

            Permission is hereby granted, free of charge,
            to any person obtaining a copy of this software.

              -f, --file       Read parse tree data from file instead of stdin.
              -v, --verbose
              --help           Display this help screen.
              --version        Display version information.
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Empty(openCli["commands"]!.AsArray());
        Assert.Contains(openCli["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--file", StringComparison.Ordinal));
        Assert.Contains(openCli["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--verbose", StringComparison.Ordinal));
    }

    [Fact]
    public void Regenerator_Cleans_CommandLineParser_Status_And_Help_Heading_Titles()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var statusVersionRoot = Path.Combine(repositoryRoot, "index", "packages", "markdownsnippets.tool", "28.0.1");
        WriteMetadata(statusVersionRoot, "MarkdownSnippets.Tool", "28.0.1", "mdsnippets", rejectedHelpArtifact: true);
        WriteCrawl(statusVersionRoot,
            """
            Finished 70ms
            mdsnippets 28.0.1+69fd87ea2028232016b5688e001b494233fa0f12
            Copyright 2026. All rights reserved

              -t, --target-directory    The target directory to run against.

              --help                    Display this help screen.

              --version                 Display version information.
            """);

        var helpVersionRoot = Path.Combine(repositoryRoot, "index", "packages", "docfx2xml", "1.0.4");
        WriteMetadata(helpVersionRoot, "Docfx2xml", "1.0.4", "docfx2xml", rejectedHelpArtifact: true);
        WriteCrawl(helpVersionRoot,
            """
            HELP:

              init       (Default Verb) initialize new convertConfig.json file if not exist
              run_j      Run convert docfx yaml to xml, using json config file(convertConfig.json)
              run_x      Run convert docfx yaml to xml, using xml config file(ConvertConfig.xml)
              run_a      Run convert docfx yaml to xml, using cmd args as config values
              help       Display more information on a specific command.
              version    Display version information.

            Docfx2xml 1.0.0
            Opensquiggly.com
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(2, result.CandidateCount);
        Assert.Equal(2, result.RewrittenCount);

        var markdownOpenCli = ParseJsonObject(Path.Combine(statusVersionRoot, "opencli.json"));
        Assert.Equal("mdsnippets", markdownOpenCli["info"]?["title"]?.GetValue<string>());
        Assert.Equal("28.0.1+69fd87ea2028232016b5688e001b494233fa0f12", markdownOpenCli["info"]?["version"]?.GetValue<string>());

        var docfxOpenCli = ParseJsonObject(Path.Combine(helpVersionRoot, "opencli.json"));
        Assert.Equal("Docfx2xml", docfxOpenCli["info"]?["title"]?.GetValue<string>());
        Assert.Contains(docfxOpenCli["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "init", StringComparison.Ordinal));
        Assert.Contains(docfxOpenCli["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "run_j", StringComparison.Ordinal));
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
