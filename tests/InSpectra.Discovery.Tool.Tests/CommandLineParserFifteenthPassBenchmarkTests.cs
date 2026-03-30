namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserFifteenthPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Infers_Current_Noun_Phrase_Value_Options()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.current-clp-value-phrases", "1.0.0");
        WriteMetadata(versionRoot, "sample.current-clp-value-phrases", "1.0.0", "sample-current-clp-value-phrases", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            sample-current-clp-value-phrases 1.0.0

              --header           The header to write.

              --convention       Controls the target document convention.
                                 Supported values: SourceTransform, InPlaceOverwrite.

              --max-width        Controls the maximum character width for snippets.
                                 Must be positive.

              --queue            Queue name

              --before           Only include messages enqueued before this UTC datetime

              --categorize-by    Properties to categorize by.

              --file-version     Use to set the version for a binary going into an app cast.

              --GameRelease      Game release that the plugin is related to

              --BackupDays       Days to keep backup plugins in the temp folder

              --to               The directory to export the files to.

              --title            Friendly name of announcement

              --labels           Labels to add to the PR

              --meta-reference   Free-form reference string stored in metadata.

              --help             Display this help screen.

              --version          Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.NotNull(FindOption(options, "--header")!["arguments"]);
        Assert.NotNull(FindOption(options, "--convention")!["arguments"]);
        Assert.NotNull(FindOption(options, "--max-width")!["arguments"]);
        Assert.NotNull(FindOption(options, "--queue")!["arguments"]);
        Assert.NotNull(FindOption(options, "--before")!["arguments"]);
        Assert.NotNull(FindOption(options, "--categorize-by")!["arguments"]);
        Assert.NotNull(FindOption(options, "--file-version")!["arguments"]);
        Assert.NotNull(FindOption(options, "--GameRelease")!["arguments"]);
        Assert.NotNull(FindOption(options, "--BackupDays")!["arguments"]);
        Assert.NotNull(FindOption(options, "--to")!["arguments"]);
        Assert.NotNull(FindOption(options, "--title")!["arguments"]);
        Assert.NotNull(FindOption(options, "--labels")!["arguments"]);
        Assert.NotNull(FindOption(options, "--meta-reference")!["arguments"]);
    }

    [Fact]
    public void Regenerator_Preserves_Current_Boolean_Flags_Near_New_Value_Phrases()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.current-clp-bool-phrases", "1.0.0");
        WriteMetadata(versionRoot, "sample.current-clp-bool-phrases", "1.0.0", "sample-current-clp-bool-phrases", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            sample-current-clp-bool-phrases 1.0.0

              --write-header     Write a header at the top of each resultant md file.

              --all              Export all languages.

              --Debug            Set up for debug mode, including resetting nuget caches

              --merge-similar    Merge similar DLQ categories using clustering

              --help             Display this help screen.

              --version          Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();

        Assert.Null(FindOption(options, "--write-header")!["arguments"]);
        Assert.Null(FindOption(options, "--all")!["arguments"]);
        Assert.Null(FindOption(options, "--Debug")!["arguments"]);
        Assert.Null(FindOption(options, "--merge-similar")!["arguments"]);
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


