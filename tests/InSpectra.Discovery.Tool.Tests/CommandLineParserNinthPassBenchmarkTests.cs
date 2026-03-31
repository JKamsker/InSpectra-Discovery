namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Artifacts;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserNinthPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Infers_Value_Options_From_Name_Number_Type_And_Parameter_Phrases()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.commandlineparser", "1.0.0");
        WriteMetadata(versionRoot, "Sample.CommandLineParser", "1.0.0", "sample", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            sample 1.0.0

              --variableString      Variable definition string.

              --master              Name of the master template file.

              --pattern             File name search pattern ('*' for wildcards)

              --offset              The number of days to offset from today.

              --communicationType   Type of communication to use (e.g., STDInOut,
                                    NamedPipes).

              --caller              Parameters for the Caller Communications Extensions
                                    (JSON format).

              --type                Override type of parse.

              --max-fetch-bytes     max fetch bytes when read source database (unit
                                    KB), default value is 10 (100KB).

              --attributes-max      Override: max attributes per line.

              --help                Display this help screen.

              --version             Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.NotNull(FindOption(options, "--variableString")!["arguments"]);
        Assert.NotNull(FindOption(options, "--master")!["arguments"]);
        Assert.NotNull(FindOption(options, "--pattern")!["arguments"]);
        Assert.NotNull(FindOption(options, "--offset")!["arguments"]);
        Assert.NotNull(FindOption(options, "--communicationType")!["arguments"]);
        Assert.NotNull(FindOption(options, "--caller")!["arguments"]);
        Assert.NotNull(FindOption(options, "--type")!["arguments"]);
        Assert.NotNull(FindOption(options, "--max-fetch-bytes")!["arguments"]);
        Assert.NotNull(FindOption(options, "--attributes-max")!["arguments"]);
    }

    [Fact]
    public void Regenerator_Does_Not_Infer_Value_Arguments_From_Gather_Delete_Save_Use_Or_Search_Flag_Phrases()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.asa", "1.0.0");
        WriteMetadata(versionRoot, "Sample.Asa", "1.0.0", "asa", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            asa 1.0.0

              --keys                Gather information about the cryptographic keys on
                                    the system.

              --reset-database      Delete the output database

              --savetodatabase      Save to internal database for review in GUI

              --autodetect          Use the AutoDetect parser

              --recursive           Search directories recursively.

              --gatherverboselogs   Gather all levels in the Log collector.

              --shards              Number of Database Shards to use.

              --help                Display this help screen.

              --version             Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.Null(FindOption(options, "--keys")!["arguments"]);
        Assert.Null(FindOption(options, "--reset-database")!["arguments"]);
        Assert.Null(FindOption(options, "--savetodatabase")!["arguments"]);
        Assert.Null(FindOption(options, "--autodetect")!["arguments"]);
        Assert.Null(FindOption(options, "--recursive")!["arguments"]);
        Assert.Null(FindOption(options, "--gatherverboselogs")!["arguments"]);
        Assert.NotNull(FindOption(options, "--shards")!["arguments"]);
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


