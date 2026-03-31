namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Artifacts;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserEighthPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Prefers_Usage_Placeholders_Over_Generic_Value_Position_Rows()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "trdelete", "0.22.0");
        WriteMetadata(versionRoot, "trdelete", "0.22.0", "trdelete", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            trdelete

            ## Usage

                trdelete <string>

              --help           Display this help screen.

              --version        Display version information.

              value pos. 0
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var argument = Assert.Single(openCli["arguments"]!.AsArray());

        Assert.Equal("STRING", argument!["name"]!.GetValue<string>());
        Assert.True(argument["required"]!.GetValue<bool>());
        Assert.Equal(1, argument["arity"]!["minimum"]!.GetValue<int>());
        Assert.Equal(1, argument["arity"]!["maximum"]!.GetValue<int>());
    }

    [Fact]
    public void Regenerator_Infers_Sequence_Arguments_From_CommandLineParser_Plus_Usage()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "trwdog", "0.23.43");
        WriteMetadata(versionRoot, "trwdog", "0.23.43", "trwdog", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            trwdog

            ## Usage

                trwdog <arg>+

              --help           Display this help screen.

              --version        Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var argument = Assert.Single(openCli["arguments"]!.AsArray());

        Assert.Equal("ARG", argument!["name"]!.GetValue<string>());
        Assert.True(argument["required"]!.GetValue<bool>());
        Assert.Equal(1, argument["arity"]!["minimum"]!.GetValue<int>());
        Assert.Null(argument["arity"]!["maximum"]);
    }

    [Fact]
    public void Regenerator_Infers_Bare_Bracketed_Usage_Arguments_And_Sequences()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "triconv", "0.23.43");
        WriteMetadata(versionRoot, "triconv", "0.23.43", "triconv", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            triconv

            ## Usage

                triconv [-f ENCODING] [-t ENCODING] [INPUTFILE...]

              --help           Display this help screen.

              --version        Display version information.

              value pos. 0
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var argument = Assert.Single(openCli["arguments"]!.AsArray());

        Assert.Equal("INPUTFILE", argument!["name"]!.GetValue<string>());
        Assert.False(argument["required"]!.GetValue<bool>());
        Assert.Equal(0, argument["arity"]!["minimum"]!.GetValue<int>());
        Assert.Null(argument["arity"]!["maximum"]);
    }

    [Fact]
    public void Regenerator_Drops_Generic_Value_Position_Rows_When_Usage_Is_Argument_Free()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "trfoldlit", "0.23.43");
        WriteMetadata(versionRoot, "trfoldlit", "0.23.43", "trfoldlit", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            trfoldlit

            ## Usage

                trfoldlit

              --help           Display this help screen.

              --version        Display version information.

              value pos. 0
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Null(openCli["arguments"]);
    }

    [Fact]
    public void Regenerator_Infers_Value_Options_From_CommandLineParser_Hard_Description_Hints()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "coveragechecker.commandline", "0.9.0");
        WriteMetadata(versionRoot, "CoverageChecker.CommandLine", "0.9.0", "CoverageChecker.CommandLine", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            CoverageChecker.CommandLine 0.9.0

              -i, --include             Glob patterns of files to include in the coverage
                                        analysis.

              -e, --exclude             Glob patterns of files to exclude from the coverage
                                        analysis.

              -l, --line-threshold      Line coverage threshold (percentage). Default: 80

              -b, --branch-threshold    Branch coverage threshold (percentage). Default: 80

              --help                    Display this help screen.

              --version                 Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.NotNull(FindOption(options, "--include")!["arguments"]);
        Assert.NotNull(FindOption(options, "--exclude")!["arguments"]);
        Assert.NotNull(FindOption(options, "--line-threshold")!["arguments"]);
        Assert.NotNull(FindOption(options, "--branch-threshold")!["arguments"]);
    }

    [Fact]
    public void Regenerator_Does_Not_Require_Defaulted_Explicit_Value_Options_Or_Invent_Boolean_Flag_Arguments()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "dotnet-codemetric", "1.0.1");
        WriteMetadata(versionRoot, "dotnet-codemetrics", "1.0.1", "dotnet-codemetrics", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            dotnet-codemetrics 1.0.1

              -v, --verbose               (Default: false) Set output to verbose messages.

              -p, --project directory     (Default: Current Directory) Set the project path.
                                          <project directory path>

              -s, --solution directory    (Default: Current Directory) Set the solution
                                          path. <solution directory path>

              -o, --output directory      (Default: Current Directory) Set the output
                                          directory for generated code metric xml

              --help                      Display this help screen.

              --version                   Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();
        var verbose = FindOption(options, "--verbose");
        var project = FindOption(options, "--project");
        var solution = FindOption(options, "--solution");
        var output = FindOption(options, "--output");

        Assert.Null(verbose!["arguments"]);
        Assert.False(project!["arguments"]![0]!["required"]!.GetValue<bool>());
        Assert.False(solution!["arguments"]![0]!["required"]!.GetValue<bool>());
        Assert.False(output!["arguments"]![0]!["required"]!.GetValue<bool>());
    }

    [Fact]
    public void Regenerator_Infers_Override_Value_Options_Without_Inventing_Nearby_Flags()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "xamlstyler.console", "3.2501.8");
        WriteMetadata(versionRoot, "xstyler", "3.2501.8", "xstyler", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            xstyler 3.2501.8

              --indent-size                        Override: indent size.

              --indent-tabs                        Override: indent with tabs.

              --attributes-max-chars               Override: max attribute characters per
                                                   line.

              --help                               Display this help screen.

              --version                            Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.NotNull(FindOption(options, "--indent-size")!["arguments"]);
        Assert.Null(FindOption(options, "--indent-tabs")!["arguments"]);
        Assert.NotNull(FindOption(options, "--attributes-max-chars")!["arguments"]);
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


