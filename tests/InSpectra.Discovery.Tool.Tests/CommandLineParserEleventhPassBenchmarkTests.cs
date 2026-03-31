namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Artifacts;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserEleventhPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Parses_Mixed_Short_Long_Value_Signatures()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "formatconverter", "2.1.0");
        WriteMetadata(versionRoot, "FormatConverter", "2.1.0", "formatconverter", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            FormatConverter 2.1.0

              -i FILE, --input=FILE            Required. Path to input file (or '-' for
                                               stdin)

              -o FILE, --output=FILE           Output file path (if not specified, will be
                                               auto-generated based on input file name)

              -v LEVEL, --verbosity=LEVEL      Set verbosity level (None, Error, Warning,
                                               Info, Debug, Trace)

              --help                           Display this help screen.

              --version                        Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.NotNull(FindOption(options, "--input")!["arguments"]);
        Assert.NotNull(FindOption(options, "--output")!["arguments"]);
        Assert.NotNull(FindOption(options, "--verbosity")!["arguments"]);
    }

    [Fact]
    public void Regenerator_Does_Not_Treat_Wrapped_Usage_Value_Continuations_As_Positionals()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "midirec", "1.2.0-beta03");
        WriteMetadata(versionRoot, "midirec", "1.2.0-beta03", "midirec", rejectedHelpArtifact: true);
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
                            midirec 1.2.0-beta03

                              record     Record MIDI input.
                              help       Display more information on a specific command.
                              version    Display version information.
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "record",
                        ["payload"] =
                            """
                            midirec 1.2.0-beta03
                            USAGE:
                            normal scenario:
                              midirec record --delay 5000 --format {Now}.mid --input M1,Triton --resolution
                              480

                              i, input         (Default: *) MIDI Input name or index
                              d, delay         (Default: 5000) Delay (in milliseconds) in silence before
                                               saving the latest recorded MIDI events
                              f, format        (Default: {Now:yyyyMMddHHmmss}.mid) Format String for output
                                               MIDI path
                              r, resolution    (Default: 480) MIDI resolution in pulses per quarter note
                                               (PPQN)
                              p, dump          Dump input into dump file (at the current dir)
                              help             Display more information on a specific command.
                              version          Display version information.
                            """,
                    },
                },
            });

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var record = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "record", StringComparison.Ordinal)));

        Assert.Null(record!["arguments"]);
        Assert.NotNull(FindOption(record["options"]!.AsArray(), "--input")!["arguments"]);
    }

    [Fact]
    public void Regenerator_Balances_Current_CommandLineParser_Value_And_Flag_Phrases()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.current-clp", "1.0.0");
        WriteMetadata(versionRoot, "sample.current-clp", "1.0.0", "sample-current-clp", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            sample-current-clp 1.0.0

              -o, --output                  Output file location.

              --show-source-context         Append source context to references

              -x, --regex                   Allow regex.

              -r, --replace                 Replace string.

              -f, --file-extract-version    (Default: false) Determine the version
                                            from the file path.

              -e, --ext                     Search for file extensions. Can be
                                            something like "exe" or "exe,msi".

              -b, --buildconfig             (Default: Release) Build configuration.

              --runtime                     Packs the application with a specified
                                            .Net runtime (win-x64, linux-x64,
                                            etc.).

              --Root                        Start folder where the search is
                                            performed.

              --Ignores                     Ignore these folder names in this
                                            comma-separated list.

              --open                        (Default: ) URL extension to launch
                                            http://localhost:<port>/%s.

              --help                        Display this help screen.

              --version                     Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.NotNull(FindOption(options, "--output")!["arguments"]);
        Assert.Null(FindOption(options, "--show-source-context")!["arguments"]);
        Assert.NotNull(FindOption(options, "--replace")!["arguments"]);
        Assert.Null(FindOption(options, "--regex")!["arguments"]);
        Assert.Null(FindOption(options, "--file-extract-version")!["arguments"]);
        Assert.NotNull(FindOption(options, "--ext")!["arguments"]);
        Assert.NotNull(FindOption(options, "--buildconfig")!["arguments"]);
        Assert.NotNull(FindOption(options, "--runtime")!["arguments"]);
        Assert.NotNull(FindOption(options, "--Root")!["arguments"]);
        Assert.NotNull(FindOption(options, "--Ignores")!["arguments"]);
        Assert.NotNull(FindOption(options, "--open")!["arguments"]);
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


