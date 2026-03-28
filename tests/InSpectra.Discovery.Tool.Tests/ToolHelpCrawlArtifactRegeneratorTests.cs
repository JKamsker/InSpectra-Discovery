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
    public void Regenerator_Falls_Back_To_Minimal_OpenCli_When_Root_Capture_Is_Unparseable()
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

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("antlr4cg", regenerated["info"]?["title"]?.GetValue<string>());
        Assert.Equal("2.3.0", regenerated["info"]?["version"]?.GetValue<string>());
        Assert.Empty(regenerated["commands"]!.AsArray());
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
