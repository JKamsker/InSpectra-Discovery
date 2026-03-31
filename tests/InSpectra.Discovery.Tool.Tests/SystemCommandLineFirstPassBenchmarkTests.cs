namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Artifacts;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class SystemCommandLineFirstPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Does_Not_Treat_Enum_Values_As_Option_Aliases()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.scl-enum-values", "1.0.0");
        WriteMetadata(versionRoot, "sample.scl-enum-values", "1.0.0", "sample-scl");
        WriteCrawlMulti(versionRoot, new[]
        {
            (command: (string?)null, payload:
            """
            Description:
              Sample SCL tool

            Usage:
              sample-scl [command] [options]

            Options:
              --version       Show version information
              -?, -h, --help  Show help and usage information

            Commands:
              generate <directoryOrFile>  Generate documentation files.
            """),
            (command: "generate", payload:
            """
            Description:
              Generate documentation files from Bicep models.

            Usage:
              sample-scl generate [<directoryOrFile>...] [options]

            Arguments:
              <directoryOrFile>  One or more paths to directories.

            Options:
              -o, --output <output>                                              The output directory. [default: docs]
              --log-level <Critical|Debug|Error|Information|None|Trace|Warning>  Specify the log level [default: Information]
              -v, --verbose                                                      Enable verbose output
              -?, -h, --help                                                     Show help and usage information
            """),
        });

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var commands = openCli["commands"]!.AsArray();
        Assert.Single(commands);

        var generateCmd = commands[0]!.AsObject();
        var options = generateCmd["options"]!.AsArray();

        // --log-level should be the primary name, NOT --Information
        var logLevelOpt = FindOption(options, "--log-level");
        Assert.NotNull(logLevelOpt);
        Assert.NotNull(logLevelOpt["arguments"]);

        // There should NOT be phantom options like --Information, --Debug, --Error, etc.
        Assert.Null(FindOption(options, "--Information"));
        Assert.Null(FindOption(options, "--Debug"));
        Assert.Null(FindOption(options, "--Error"));
        Assert.Null(FindOption(options, "--Critical"));
        Assert.Null(FindOption(options, "--Warning"));
    }

    [Fact]
    public void Regenerator_Preserves_Pipe_Delimited_Option_Aliases()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.scl-pipe-aliases", "1.0.0");
        WriteMetadata(versionRoot, "sample.scl-pipe-aliases", "1.0.0", "sample-pipe");
        WriteCrawl(versionRoot, null,
            """
            sample-pipe 1.0.0

              -v | --verbose    Enable verbose output
              -o | --output     Output path
              --help            Display this help screen.
              --version         Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        // -v | --verbose should be parsed as --verbose with alias -v
        var verbose = FindOption(options, "--verbose");
        Assert.NotNull(verbose);
        Assert.Contains("-v", verbose["aliases"]!.AsArray().Select(a => a!.GetValue<string>()));
    }

    [Fact]
    public void Regenerator_Uses_Command_Name_When_Title_Is_Description()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.scl-desc-title", "1.0.0");
        WriteMetadata(versionRoot, "sample.scl-desc-title", "1.0.0", "aspirate");
        WriteCrawl(versionRoot, null,
            """
            Handle deployments of a .NET Aspire AppHost

            Description:

            Usage:
              aspirate [command] [options]

            Options:
              --version       Show version information
              -?, -h, --help  Show help and usage information

            Commands:
              apply     Apply the generated manifest to the cluster.
              build     Build containers for the project.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));

        // Title should be the command name, not the description sentence
        Assert.Equal("aspirate", openCli["info"]!["title"]!.GetValue<string>());

        // The description sentence should move to info.description
        Assert.Equal("Handle deployments of a .NET Aspire AppHost",
            openCli["info"]!["description"]!.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Keeps_Product_Name_Title()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.scl-product-title", "1.0.0");
        WriteMetadata(versionRoot, "sample.scl-product-title", "1.0.0", "dotnet-lambda");
        WriteCrawl(versionRoot, null,
            """
            Amazon Lambda Tools for .NET Core applications (6.0.5)

              --help     Display this help screen.
              --version  Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));

        // "Amazon Lambda Tools" is a product name, not a description — keep it (version is part of title)
        var title = openCli["info"]!["title"]!.GetValue<string>();
        Assert.StartsWith("Amazon Lambda Tools", title);
    }

    private static JsonObject? FindOption(JsonArray options, string name)
        => options
            .OfType<JsonObject>()
            .FirstOrDefault(option => string.Equals(option["name"]?.GetValue<string>(), name, StringComparison.Ordinal));

    private static void WriteMetadata(string versionRoot, string packageId, string version, string command)
    {
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = packageId,
                ["version"] = version,
                ["command"] = command,
                ["cliFramework"] = "System.CommandLine",
                ["status"] = "partial",
                ["analysisMode"] = "help",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = (string?)null,
                        ["classification"] = "invalid-opencli-artifact",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliSource"] = (string?)null,
                },
            });
    }

    private static void WriteCrawl(string versionRoot, string? command, string payload)
    {
        WriteCrawlMulti(versionRoot, new[] { (command, payload) });
    }

    private static void WriteCrawlMulti(string versionRoot, IEnumerable<(string? command, string payload)> entries)
    {
        var commands = new JsonArray();
        foreach (var (command, payload) in entries)
        {
            commands.Add(new JsonObject
            {
                ["command"] = command,
                ["payload"] = payload,
            });
        }

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "crawl.json"),
            new JsonObject { ["commands"] = commands });
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


