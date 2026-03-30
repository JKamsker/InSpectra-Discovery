namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserDuplicateOptionBenchmarkTests
{
    [Fact]
    public void Regenerator_Merges_CommandLineParser_Usage_And_Option_Table_Duplicates()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "trparse", "0.23.43");
        WriteMetadata(versionRoot, "trparse", "0.23.43", "trparse", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            trparse
            Copyright (c) 2023 Ken Domino
            ## Summary
            Parse a grammar or use generated parse to parse input
            ## Usage

                trparse (<string> | <options>)*
                -i, --input      Parse the given string as input.
                -t, --type       Specifies type of grammar: ANTLRv4, ANTLRv3, ANTLRv2,
                Bison, rex, pegen_v3_10
                -p, --parser     Location of pre-built parser (aka the trgen Generated/
                directory)

              -d, --dll         Search for parser in dll with this specified name.
              -i, --input       Parse input string.
              -p, --parser      Location of pre-built parser (aka the trgen Generated/
                                directory)
              --help            Display this help screen.
              --version         Display version information.
              value pos. 0
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var options = openCli["options"]!.AsArray();
        Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--input", StringComparison.Ordinal)));
        Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--parser", StringComparison.Ordinal)));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("ok", metadata["status"]?.GetValue<string>());
        Assert.Equal("help-crawl", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Merges_CommandLineParser_BuiltIn_Version_Rows_With_Trailing_Positional_Noise()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "trglob", "0.23.43");
        WriteMetadata(versionRoot, "trglob", "0.23.43", "trglob", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            trglob
            Copyright (c) 2023 Ken Domino
            ## Summary
            Expand a glob string into file names.
            ## Usage

                trgen <string>+

              -v, --verbose
              --full
              --version
              --help           Display this help screen.
              --version        Display version information.
              value pos. 0
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var versionOptions = openCli["options"]!.AsArray()
            .Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--version", StringComparison.Ordinal))
            .ToArray();
        var versionOption = Assert.Single(versionOptions);
        Assert.Equal("Display version information.", versionOption!["description"]?.GetValue<string>());

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("ok", metadata["status"]?.GetValue<string>());
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


