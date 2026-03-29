using System.Text.Json.Nodes;
using Xunit;

public sealed class ToolHelpCrawlArtifactRegeneratorTests
{
    [Fact]
    public void Regenerates_Generic_Help_OpenCli_From_Stored_Crawls()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var genericVersionRoot = Path.Combine(repositoryRoot, "index", "packages", "registerbot", "2.0.20");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(genericVersionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "RegisterBot",
                ["version"] = "2.0.20",
                ["command"] = "RegisterBot",
                ["cliFramework"] = null,
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(genericVersionRoot, "crawl.json"),
            new JsonObject
            {
                ["documentCount"] = 781,
                ["commandCount"] = 781,
                ["captureCount"] = 2,
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = null,
                        ["payload"] =
                            """
                            RegisterBot Version 2.0.20.0

                            ```RegisterBot [--endpoint endpoint] [--name botName] [--resource-group groupName] [--help]```

                            Creates or updates a bot registration for [botName] pointing to [endpoint] with teams channel and SSO enabled.

                            | Argument                         | Description                                                                                   |
                            | -------------------------------- | --------------------------------------------------------------------------------------------- |
                            | -e, --endpoint endpoint          | (optional) If not specified the endpoint will stay the same as project settings               |
                            | -n, --name botName               | (optional) If not specified the botname will be pulled from settings or interactively asked   |
                            | -g, --resource-group groupName   | (optional) If not specified the groupname will be pulled from settings or interactively asked |
                            | -v, --verbose                    | (optional) show all commands as they are executed                                             |
                            | -h, --help                       | display help                                                                                  |
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "| Argument",
                        ["payload"] =
                            """
                            RegisterBot Version 2.0.20.0

                            ```RegisterBot [--endpoint endpoint] [--name botName] [--resource-group groupName] [--help]```

                            Creates or updates a bot registration for [botName] pointing to [endpoint] with teams channel and SSO enabled.

                            | Argument                         | Description                                                                                   |
                            | -------------------------------- | --------------------------------------------------------------------------------------------- |
                            | -e, --endpoint endpoint          | (optional) If not specified the endpoint will stay the same as project settings               |
                            | -n, --name botName               | (optional) If not specified the botname will be pulled from settings or interactively asked   |
                            | -g, --resource-group groupName   | (optional) If not specified the groupname will be pulled from settings or interactively asked |
                            | -v, --verbose                    | (optional) show all commands as they are executed                                             |
                            | -h, --help                       | display help                                                                                  |
                            """,
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(genericVersionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-help",
                    ["helpDocumentCount"] = 781,
                },
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "| Argument",
                        ["hidden"] = false,
                    },
                },
            });

        var clifxVersionRoot = Path.Combine(repositoryRoot, "index", "packages", "sampleclifx", "1.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(clifxVersionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "SampleCliFx",
                ["version"] = "1.0.0",
                ["command"] = "sample",
                ["cliFramework"] = "CliFx",
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(clifxVersionRoot, "crawl.json"),
            new JsonObject
            {
                ["commands"] = new JsonArray(),
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(clifxVersionRoot, "opencli.json"),
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
                        ["name"] = "sample",
                        ["hidden"] = false,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(2, result.ScannedCount);
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.UnchangedCount);
        Assert.Equal(0, result.FailedCount);

        var regenerated = ParseJsonObject(Path.Combine(genericVersionRoot, "opencli.json"));
        Assert.Equal("crawled-from-help", regenerated["x-inspectra"]?["artifactSource"]?.GetValue<string>());
        Assert.Equal(1, regenerated["x-inspectra"]?["helpDocumentCount"]?.GetValue<int>());
        Assert.Contains(regenerated["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--endpoint", StringComparison.Ordinal));
        Assert.Empty(regenerated["commands"]!.AsArray());

        var untouchedCliFx = ParseJsonObject(Path.Combine(clifxVersionRoot, "opencli.json"));
        Assert.Equal("crawled-from-clifx-help", untouchedCliFx["x-inspectra"]?["artifactSource"]?.GetValue<string>());
        Assert.Single(untouchedCliFx["commands"]!.AsArray());
    }

    [Fact]
    public void Regenerator_Drops_CommandLineParser_Pseudo_Verbs_From_Crawled_Help()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "dotnet-certificate-tool", "2.1.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "dotnet-certificate-tool",
                ["version"] = "2.1.0",
                ["command"] = "gsoft-cert",
                ["cliFramework"] = "CommandLineParser",
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
                            Error parsing
                             CommandLine.HelpVerbRequestedError
                            GSoft.CertificateTool 1.0.0+bb4d252c46ae13f3169853b02995b8cd77635ab6
                            Copyright (C) 2026 GSoft.CertificateTool

                              add        Installs a pfx certificate to selected store.
                              remove     Removes a pfx certificate from selected store.
                              version    Display version information.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "CommandLine.HelpVerbRequestedError",
                        ["payload"] =
                            """
                            Error parsing
                             CommandLine.BadVerbSelectedError
                            GSoft.CertificateTool 1.0.0+bb4d252c46ae13f3169853b02995b8cd77635ab6
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "add",
                        ["payload"] =
                            """
                            Error parsing
                             CommandLine.HelpRequestedError
                            GSoft.CertificateTool 1.0.0+bb4d252c46ae13f3169853b02995b8cd77635ab6

                              -f, --file              The certificate file.
                              --help                  Display this help screen.
                            """,
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
                    ["helpDocumentCount"] = 99,
                },
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "CommandLine.HelpVerbRequestedError",
                        ["hidden"] = false,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("GSoft.CertificateTool", regenerated["info"]?["title"]?.GetValue<string>());
        Assert.DoesNotContain(regenerated["commands"]!.AsArray(), command =>
            string.Equals(command?["name"]?.GetValue<string>(), "CommandLine.HelpVerbRequestedError", StringComparison.Ordinal));
        Assert.Contains(regenerated["commands"]!.AsArray(), command =>
            string.Equals(command?["name"]?.GetValue<string>(), "add", StringComparison.Ordinal)
            && command?["options"] is JsonArray);
    }

    [Fact]
    public void Regenerator_Prefers_Stored_Single_Stream_Help_Payload_Over_Invocation_Echo_Combination()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "spriggit.yaml.skyrim", "0.41.0-alpha.5");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "spriggit.yaml.skyrim",
                ["version"] = "0.41.0-alpha.5",
                ["command"] = "Spriggit.Yaml.Skyrim",
                ["cliFramework"] = "CommandLineParser",
                ["analysisMode"] = "help",
                ["status"] = "partial",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["classification"] = "invalid-opencli-artifact",
                    },
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
                        ["helpInvocation"] = "--help",
                        ["payload"] =
                            """
                            Spriggit version 0.41.0
                            --help
                            Spriggit.Yaml.Skyrim 0.41.0
                            2024

                              serialize, convert-from-plugin    Converts a plugin to text.

                              help                              Display more information on a specific command.
                            """,
                        ["result"] = new JsonObject
                        {
                            ["stdout"] =
                                """
                                Spriggit version 0.41.0
                                --help
                                """,
                            ["stderr"] =
                                """
                                Spriggit.Yaml.Skyrim 0.41.0
                                2024

                                  serialize, convert-from-plugin    Converts a plugin to text.

                                  help                              Display more information on a specific command.
                                """,
                            ["exitCode"] = 1,
                            ["timedOut"] = false,
                            ["status"] = "failed",
                            ["durationMs"] = 1,
                        },
                    },
                    new JsonObject
                    {
                        ["command"] = "convert-from-plugin",
                        ["helpInvocation"] = "convert-from-plugin --help",
                        ["payload"] =
                            """
                            Spriggit.Yaml.Skyrim 0.41.0

                              -i, --InputPath    Required. Path to the plugin.
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("Spriggit.Yaml.Skyrim", regenerated["info"]?["title"]?.GetValue<string>());
        Assert.Contains(regenerated["commands"]!.AsArray(), command => string.Equals(command?["name"]?.GetValue<string>(), "convert-from-plugin", StringComparison.Ordinal));
    }

    [Fact]
    public void Regenerator_DoesNot_Nest_Builtin_Auxiliary_Inventory_Echoes()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "spriggit.yaml.skyrim", "0.41.0-alpha.5");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "spriggit.yaml.skyrim",
                ["version"] = "0.41.0-alpha.5",
                ["command"] = "Spriggit.Yaml.Skyrim",
                ["cliFramework"] = "CommandLineParser",
                ["analysisMode"] = "help",
                ["status"] = "partial",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["classification"] = "invalid-opencli-artifact",
                    },
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
                            Spriggit.Yaml.Skyrim 0.41.0

                              serialize, convert-from-plugin    Converts a plugin to text.

                              help                              Display more information on a specific command.

                              version                           Display version information.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "convert-from-plugin",
                        ["payload"] =
                            """
                            Spriggit.Yaml.Skyrim 0.41.0

                              -i, --InputPath    Required. Path to the plugin.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "help",
                        ["payload"] =
                            """
                            Spriggit.Yaml.Skyrim 0.41.0

                              serialize, convert-from-plugin    Converts a plugin to text.

                              help                              Display more information on a specific command.

                              version                           Display version information.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "help convert-from-plugin",
                        ["payload"] =
                            """
                            Spriggit.Yaml.Skyrim 0.41.0

                              -i, --InputPath    Required. Path to the plugin.
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var help = Assert.Single(regenerated["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "help", StringComparison.Ordinal)));
        Assert.Null(help!["commands"]);
    }

    [Fact]
    public void Regenerator_Rejects_Unparseable_Root_Capture_As_Invalid_OpenCli()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "antlr4codegenerator.tool", "2.3.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "antlr4codegenerator.tool",
                ["version"] = "2.3.0",
                ["command"] = "antlr4cg",
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
                            Executing: java -jar /tmp/tool/antlr-4.13.1-complete.jar --help
                            Error: error(2):  unknown command-line option --help
                            """,
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
                },
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "Error: error(2):",
                        ["hidden"] = false,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("partial", metadata["status"]?.GetValue<string>());
        Assert.Null(metadata["artifacts"]?["opencliPath"]);
        Assert.Equal("invalid-opencli-artifact", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Rejects_Interactive_Error_Output_As_Invalid_OpenCli()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "b2cconsoleclient", "1.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "b2cconsoleclient",
                ["version"] = "1.0.0",
                ["command"] = "b2cconsoleclient",
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
                            Azure B2C Console Client
                            ========================
                            Configuration is missing or incomplete. Let's set it up:

                            Error: The authority (including the tenant ID) must be in a well-formed URI format.  (Parameter 'authority')
                            Details: System.ArgumentException: The authority (including the tenant ID) must be in a well-formed URI format.  (Parameter 'authority')
                               at B2CConsoleClient.AuthenticationService..ctor(AuthConfig config) in /Users/test/B2CConsoleClient/AuthenticationService.cs:line 31
                               at B2CConsoleClient.Program.Main(String[] args) in /Users/test/B2CConsoleClient/Program.cs:line 19

                            Press any key to exit...
                            Unhandled exception. System.InvalidOperationException: Cannot read keys when either application does not have a console or when console input has been redirected. Try Console.Read.
                            """,
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

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.False(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("partial", metadata["status"]?.GetValue<string>());
        Assert.Equal("invalid-output", metadata["introspection"]?["opencli"]?["status"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", metadata["introspection"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Repairs_Stale_Help_Metadata_When_OpenCli_Is_Already_Current()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "samplehelp", "1.0.0");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var crawlPath = Path.Combine(versionRoot, "crawl.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            metadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "SampleHelp",
                ["version"] = "1.0.0",
                ["command"] = "samplehelp",
                ["status"] = "partial",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "tool-output",
                    },
                },
                ["introspection"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["classification"] = "json-ready",
                        ["artifactSource"] = "tool-output",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/samplehelp/1.0.0/metadata.json",
                    ["opencliPath"] = "index/packages/samplehelp/1.0.0/opencli.json",
                    ["opencliSource"] = "tool-output",
                    ["crawlPath"] = "index/packages/samplehelp/1.0.0/crawl.json",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            crawlPath,
            new JsonObject
            {
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = null,
                        ["payload"] =
                            """
                            samplehelp 1.0.0

                            Usage: samplehelp [options]

                            Options:
                              --verbose  Verbose output.
                            """,
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "samplehelp",
                    ["version"] = "1.0.0",
                },
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-help",
                    ["generator"] = "InSpectra.Discovery",
                    ["helpDocumentCount"] = 1,
                },
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--verbose",
                        ["recursive"] = false,
                        ["hidden"] = false,
                        ["description"] = "Verbose output.",
                    },
                },
                ["commands"] = new JsonArray(),
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("ok", metadata["status"]?.GetValue<string>());
        Assert.Equal("crawled-from-help", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
        Assert.Equal("index/packages/samplehelp/1.0.0/opencli.json", metadata["steps"]?["opencli"]?["path"]?.GetValue<string>());
        Assert.Equal("crawled-from-help", metadata["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>());
        Assert.Equal("help-crawl", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.Equal("crawled-from-help", metadata["introspection"]?["opencli"]?["artifactSource"]?.GetValue<string>());
        Assert.Equal("help-crawl", metadata["introspection"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "samplehelp", "latest", "opencli.json")));
    }

    [Fact]
    public void Regenerator_Rebuilds_Generic_Help_When_OpenCli_Is_Missing_Using_Metadata_Provenance()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "samplehelp", "1.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "SampleHelp",
                ["version"] = "1.0.0",
                ["command"] = "samplehelp",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "crawled-from-help",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["crawlPath"] = "index/packages/samplehelp/1.0.0/crawl.json",
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
                            samplehelp 1.0.0

                            Usage: samplehelp [options]

                            Options:
                              --verbose  Verbose output.
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.True(File.Exists(Path.Combine(versionRoot, "opencli.json")));
        Assert.Equal("crawled-from-help", ParseJsonObject(Path.Combine(versionRoot, "opencli.json"))["x-inspectra"]?["artifactSource"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Replays_Generic_Help_From_Stored_Process_Output_When_Payload_Is_Noisy()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "samplehelp", "2.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "SampleHelp",
                ["version"] = "2.0.0",
                ["command"] = "samplehelp",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "crawled-from-help",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["crawlPath"] = "index/packages/samplehelp/2.0.0/crawl.json",
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
                        ["payload"] = "Unhandled exception. stale payload",
                        ["result"] = new JsonObject
                        {
                            ["stdout"] =
                                """
                                samplehelp 2.0.0

                                Usage: samplehelp [options]

                                Options:
                                  --verbose  Verbose output.
                                """,
                        },
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("samplehelp", regenerated["info"]?["title"]?.GetValue<string>());
        Assert.Contains(regenerated["options"]!.AsArray(), option =>
            string.Equals(option?["name"]?.GetValue<string>(), "--verbose", StringComparison.Ordinal));
    }

    [Fact]
    public void Regenerator_Prefers_Clean_Stream_Over_Mixed_Exception_Payload()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "samplehelp", "3.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "SampleHelp",
                ["version"] = "3.0.0",
                ["command"] = "samplehelp",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "crawled-from-help",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["crawlPath"] = "index/packages/samplehelp/3.0.0/crawl.json",
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
                        ["result"] = new JsonObject
                        {
                            ["stdout"] =
                                """
                                samplehelp 3.0.0

                                Usage: samplehelp [options]

                                Options:
                                  --verbose  Verbose output.
                                """,
                            ["stderr"] =
                                """
                                Unhandled exception. System.InvalidOperationException: boom
                                   at Program.Main(String[] args)
                                """,
                        },
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("samplehelp", regenerated["info"]?["title"]?.GetValue<string>());
        Assert.Contains(regenerated["options"]!.AsArray(), option =>
            string.Equals(option?["name"]?.GetValue<string>(), "--verbose", StringComparison.Ordinal));
    }

    [Fact]
    public void Regenerator_Does_Not_Attach_Root_Help_To_Subcommand_Captures()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "samplehelp", "4.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "SampleHelp",
                ["version"] = "4.0.0",
                ["command"] = "samplehelp",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "crawled-from-help",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["crawlPath"] = "index/packages/samplehelp/4.0.0/crawl.json",
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
                            samplehelp 4.0.0

                            Usage: samplehelp [command] [options]

                            Commands:
                              deploy  Deploy the sample.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "deploy",
                        ["payload"] =
                            """
                            samplehelp 4.0.0

                            Usage: samplehelp [command] [options]

                            Options:
                              --verbose  Verbose output.
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.False(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("partial", metadata["status"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
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
