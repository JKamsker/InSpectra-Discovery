namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserThirteenthPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Strips_CommandLineParser_Banner_Version_Suffix_And_Infers_Suppress_List_Values()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "acs", "1.2.0-prerelease.26169.1");
        WriteMetadata(versionRoot, "acs", "1.2.0-prerelease.26169.1", "acs", rejectedHelpArtifact: true);
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
                            ArduinoCsCompiler - Version 1.2.0.0
                            This tool is experimental - expect many missing features and that the behavior will change.
                            Active runtime version .NET 8.0.25
                            acs 1.2.0-prerelease.26169.1+9c867ff2cf01fd8c451acae6d25950ef5aa85abc
                            The .NET Foundation

                              compile    Compile and optionally upload code to a microcontroller.
                              prepare    Prepare the Arduino runtime for uploading
                              test       Run various interactive tests on the board
                              exec       Provides some direct commands to the board
                              version    Display version information.

                            Command line parsing error
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "compile",
                        ["payload"] =
                            """
                            ArduinoCsCompiler - Version 1.2.0.0
                            This tool is experimental - expect many missing features and that the behavior will change.
                            Active runtime version .NET 8.0.25
                            acs 1.2.0-prerelease.26169.1+9c867ff2cf01fd8c451acae6d25950ef5aa85abc
                            The .NET Foundation

                              -s, --suppress              Suppress the given class(es). Removes these
                                                          classes (fully qualified names) from the execution
                                                          set. Separate by ','

                              --help                      Display this help screen.

                              --version                   Display version information.
                            """,
                    },
                },
            });

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("ArduinoCsCompiler", openCli["info"]?["title"]?.GetValue<string>());

        var compile = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "compile", StringComparison.Ordinal)));
        var suppress = Assert.Single(compile!["options"]!.AsArray().Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--suppress", StringComparison.Ordinal)));
        Assert.NotNull(suppress!["arguments"]);
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


