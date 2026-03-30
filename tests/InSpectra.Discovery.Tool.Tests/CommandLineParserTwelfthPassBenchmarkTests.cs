namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserTwelfthPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Preserves_Current_Boolean_Switch_Phrases_Without_Inventing_Arguments()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.current-clp-bools", "1.0.0");
        WriteMetadata(versionRoot, "sample.current-clp-bools", "1.0.0", "sample-current-clp-bools", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            sample-current-clp-bools 1.0.0

              --merge                         (Default: false) Merge all rewritten
                                              assemblies into a single assembly.

              --qualify                       (Default: false) In API doc
                                              generation qualify the output by
                                              the collection name.

              --printFullTypeName             Set true to print full type name

              --sort-keys                     Sort object keys alphabetically in
                                              output

              --xml-cdata                     Wrap text content in CDATA sections
                                              when necessary

              --update-inputs-to-current-sarif
                                              Update any SARIF v1 or prerelease
                                              v2 files to the current SARIF v2
                                              format.

              --overwrite-old-items           (Default: false) If--overwrite-old-items
                                              is used, this option will cause app
                                              cast items to be rewritten.

              --help                          Display this help screen.

              --version                       Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.Null(FindOption(options, "--merge")!["arguments"]);
        Assert.Null(FindOption(options, "--qualify")!["arguments"]);
        Assert.Null(FindOption(options, "--printFullTypeName")!["arguments"]);
        Assert.Null(FindOption(options, "--sort-keys")!["arguments"]);
        Assert.Null(FindOption(options, "--xml-cdata")!["arguments"]);
        Assert.Null(FindOption(options, "--update-inputs-to-current-sarif")!["arguments"]);
        Assert.Null(FindOption(options, "--overwrite-old-items")!["arguments"]);
    }

    [Fact]
    public void Regenerator_Infers_Current_Scalar_Value_Phrases_That_Were_Still_Missing()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.current-clp-values", "1.0.0");
        WriteMetadata(versionRoot, "sample.current-clp-values", "1.0.0", "sample-current-clp-values", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            sample-current-clp-values 1.0.0

              --AwsRegion                    The AWS region

              --culture                      The name of the culture to use for
                                             CultureInfo.CurrentCulture.

              --FlashSize                    Total flash size available. When
                                             specified, a matching partitions.csv
                                             file is written.

              --attributes-tolerance         Override: attributes tolerance.

              --sort                         The sort key and direction you would
                                             like to use.

              --excluded                     Package IDs that will be skipped
                                             during checking.

              --help                         Display this help screen.

              --version                      Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.NotNull(FindOption(options, "--AwsRegion")!["arguments"]);
        Assert.NotNull(FindOption(options, "--culture")!["arguments"]);
        Assert.NotNull(FindOption(options, "--FlashSize")!["arguments"]);
        Assert.NotNull(FindOption(options, "--attributes-tolerance")!["arguments"]);
        Assert.NotNull(FindOption(options, "--sort")!["arguments"]);
        Assert.NotNull(FindOption(options, "--excluded")!["arguments"]);
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


