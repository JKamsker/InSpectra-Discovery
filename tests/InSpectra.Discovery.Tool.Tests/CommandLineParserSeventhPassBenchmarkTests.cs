namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserSeventhPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Infers_Value_Options_From_Specify_Descriptions_And_Inline_Examples()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "codecov.tool", "1.13.0");
        WriteMetadata(versionRoot, "codecov", "1.13.0", "codecov", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            codecov 1.13.0+7ae0cff1f1999c619ffbbb39430d4e545f25ef14

              --branch             Specify the branch name.

              -b, --build          Specify the build number.

              -c, --sha            Specify the commit sha.

              --disable-network    Toggle functionalities. (1) --disable-network. Disable
                                   uploading the file network.

              --flag               Flag the upload to group coverage metrics. (1) --flag
                                   unittests.

              --pr                 Specify the pull request number.

              --required           Exit with 1 if not successful. Default will Exit with 0.

              --help               Display this help screen.

              --version            Display version information.
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.NotNull(FindOption(options, "--branch")!["arguments"]);
        Assert.NotNull(FindOption(options, "--build")!["arguments"]);
        Assert.NotNull(FindOption(options, "--sha")!["arguments"]);
        Assert.NotNull(FindOption(options, "--flag")!["arguments"]);
        Assert.NotNull(FindOption(options, "--pr")!["arguments"]);
        Assert.Null(FindOption(options, "--disable-network")!["arguments"]);
        Assert.Null(FindOption(options, "--required")!["arguments"]);
    }

    [Fact]
    public void Regenerator_Does_Not_Infer_Value_Arguments_For_Boolean_Default_Flags()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "ntersol.scaffold", "1.0.22");
        WriteMetadata(versionRoot, "Ntersol.Scaffold", "1.0.22", "ntersol.scaffold", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            Ntersol.Scaffold 1.0.22+b34.268e4295

              -o, --output            (Default: .\Output) change where output files are
                                      written

              --disable-database      (Default: false) disable generation of database
                                      entities

              --disable-dataaccess    (Default: false) disable generation of data access
                                      entities

              --disable-webapi        (Default: false) disable generation of web api
                                      entities

              --webapi-client         (Default: false) generate C# client for web api

              --mobile-android        (Default: false) generate clients for Android mobile
                                      platform (Java)

              --help                  Display this help screen.

              --version               Display version information.

              value pos. 0            Required. json schema file
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.NotNull(FindOption(options, "--output")!["arguments"]);
        Assert.Null(FindOption(options, "--disable-database")!["arguments"]);
        Assert.Null(FindOption(options, "--disable-dataaccess")!["arguments"]);
        Assert.Null(FindOption(options, "--disable-webapi")!["arguments"]);
        Assert.Null(FindOption(options, "--webapi-client")!["arguments"]);
        Assert.Null(FindOption(options, "--mobile-android")!["arguments"]);
    }

    [Fact]
    public void Regenerator_Infers_Format_Options_From_CommandLineParser_Examples()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "logicsoftware.easyprojects.databasemanager", "1.0.57540");
        WriteMetadata(versionRoot, "DatabaseManager", "1.0.57540", "DatabaseManager", rejectedHelpArtifact: true);
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
                            DatabaseManager 1.0.57540

                              runscript         runs specified sql script
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "runscript",
                        ["payload"] =
                            """
                            DatabaseManager 1.0.57540

                              -f, --file                 Required. Input scripts

                              -p, --param                Parameter for script in format 'name:value'. For
                                                         example -p addDemoData:1

                              --help                     Display this help screen.

                              --version                  Display version information.
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var runscript = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "runscript", StringComparison.Ordinal)));
        var param = FindOption(runscript!["options"]!.AsArray(), "--param");
        Assert.NotNull(param!["arguments"]);
    }

    [Fact]
    public void Regenerator_Does_Not_Create_Fake_Options_From_Wrapped_Option_References()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "gazel.cli", "8.0.2");
        WriteMetadata(versionRoot, "Gazel.Cli", "8.0.2", "Gazel.Cli", rejectedHelpArtifact: true);
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
                            Gazel.Cli 8.0.2

                              apigen       Generates Client API assembly from remote url or local directory

                              codegen      Generates Client API source code from remote url or local directory

                              schemagen    Generates Schema.json from remote url or local directory
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "apigen",
                        ["payload"] =
                            """
                            Gazel.Cli 8.0.2

                              --root-namespace            Root namespace of module assemblies. This value is
                                                          used only when generating from bin directory. If
                                                          not provided, first part of -n (--namespace or
                                                          --name) option will be used.

                              --help                      Display this help screen.

                              uri (pos. 0)                Required. Indicate base URL to get ApplicationModel
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "codegen",
                        ["payload"] =
                            """
                            Gazel.Cli 8.0.2

                              --root-namespace            Root namespace of module assemblies. This value is
                                                          used only when generating from bin directory. If
                                                          not provided, first part of -n (--namespace or
                                                          --name) option will be used.

                              --help                      Display this help screen.

                              uri (pos. 0)                Required. Indicate base URL to get ApplicationModel
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "schemagen",
                        ["payload"] =
                            """
                            Gazel.Cli 8.0.2

                              -n, --name                  (Default: Api) Name of the system to be represented by generated schema.

                              --help                      Display this help screen.

                              uri (pos. 0)                Required. Indicate base URL to get ApplicationModel
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var apigen = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "apigen", StringComparison.Ordinal)));
        var codegen = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "codegen", StringComparison.Ordinal)));
        var schemagen = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "schemagen", StringComparison.Ordinal)));

        Assert.DoesNotContain(apigen!["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--name", StringComparison.Ordinal));
        Assert.DoesNotContain(codegen!["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--name", StringComparison.Ordinal));
        Assert.NotNull(FindOption(apigen["options"]!.AsArray(), "--root-namespace"));
        Assert.NotNull(FindOption(codegen["options"]!.AsArray(), "--root-namespace"));
        var schemaOptions = Assert.IsType<JsonArray>(schemagen!["options"]);
        var schemaName = FindOption(schemaOptions, "--name");
        Assert.NotNull(schemaName);
    }

    private static JsonObject? FindOption(JsonArray options, string name)
        => options
            .OfType<JsonObject>()
            .FirstOrDefault(option => string.Equals(option["name"]?.GetValue<string>(), name, StringComparison.Ordinal));

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


