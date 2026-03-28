using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserBenchmarkTests
{
    [Fact]
    public void Regenerator_Reclassifies_CommandLineParser_Usage_Block_Options_And_Positionals()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "dotnet-setversion", "4.0.0");
        WriteMetadata(versionRoot, "dotnet-setversion", "4.0.0", "setversion", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            dotnet-setversion 4.0.0+bcbcc932f92f2713b506150c3694d7557d7ac35d
            Copyright 2018 ThymineC
            USAGE:
            Directory with a single csproj file:
              setversion 1.2.3
            Explicitly specifying a csproj file:
              setversion 1.2.3 MyProject.csproj
            Large repo with multiple csproj files in nested directories:
              setversion -r 1.2.3
            Pulling the version from a file:
              setversion @sem.ver

              -r, --recursive        (Default: false) Recursively search the current
                                     directory for csproj files and apply the given version
                                     to all files found. Mutually exclusive to the
                                     csprojFile argument.

              -p, --prefix           (Default: false) Set version using the VersionPrefix
                                     element for csproj files.

              --help                 Display this help screen.

              --version              Display version information.

              version (pos. 0)       Required. The version to apply to the given csproj
                                     file(s).

              csprojFile (pos. 1)    Path to a csproj file to apply the given version.
                                     Mutually exclusive to the --recursive option.
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("dotnet-setversion", openCli["info"]?["title"]?.GetValue<string>());
        Assert.Contains(openCli["options"]!.AsArray(), option => string.Equals(option?["name"]?.GetValue<string>(), "--recursive", StringComparison.Ordinal));
        Assert.Contains(openCli["arguments"]!.AsArray(), argument => string.Equals(argument?["name"]?.GetValue<string>(), "version", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(openCli["arguments"]!.AsArray(), argument => string.Equals(argument?["name"]?.GetValue<string>(), "csprojFile", StringComparison.OrdinalIgnoreCase));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("ok", metadata["status"]?.GetValue<string>());
        Assert.Equal("help-crawl", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Ignores_Indented_Alias_Mentions_Inside_CommandLineParser_Descriptions()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "acs", "1.2.0-prerelease.26169.1");
        WriteMetadata(versionRoot, "acs", "1.2.0-prerelease.26169.1", "dotnet-acs");
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
                            """,
                    },
                    new JsonObject
                    {
                        ["command"] = "prepare",
                        ["payload"] =
                            """
                            ArduinoCsCompiler - Version 1.2.0.0
                            This tool is experimental - expect many missing features and that the behavior will change.
                            Active runtime version .NET 8.0.25
                            acs 1.2.0-prerelease.26169.1+9c867ff2cf01fd8c451acae6d25950ef5aa85abc
                            The .NET Foundation

                              -t, --targetpath    Target path for the generated files. Defaults to the
                                                  Arduino workspace directory

                              --FlashSize         Total flash size available.

                              -v, --verbose       Output verbose messages.

                              -q, --quiet         (Default: false) Minimal output only. This is ignored if
                                                  -v is specified

                              --no-progress       (Default: false) Suppress printing progress messages

                              --help              Display this help screen.

                              --version           Display version information.
                            """,
                    },
                },
            });

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var prepare = Assert.Single(openCli["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "prepare", StringComparison.Ordinal)));
        var prepareOptions = prepare!["options"]!.AsArray();
        Assert.Contains(prepareOptions, option => string.Equals(option?["name"]?.GetValue<string>(), "--verbose", StringComparison.Ordinal));
        Assert.DoesNotContain(prepareOptions, option => string.Equals(option?["name"]?.GetValue<string>(), "-v", StringComparison.Ordinal));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("ok", metadata["status"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Deduplicates_CommandLineParser_BuiltIn_Version_Switch_Collisions()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "ezversionupdate", "6.0.0");
        WriteMetadata(versionRoot, "EzVersionUpdate", "6.0.0", "EzVersionUpdate");
        WriteCrawl(versionRoot,
            """
            EzVersionUpdate 6.0.0
            Copyright (C) 2026 EzVersionUpdate

              -t, --test         (Default: false) Run this as test with no file change.

              -v, --version      Will cause the application to mark all project files with
                                 this version number

              -p, --path         Path to file to affect or a directory to recursively search
                                 for project files to affect

              --help             Display this help screen.

              --version          Display version information.
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
        Assert.Contains(versionOption!["aliases"]!.AsArray(), alias => string.Equals(alias?.GetValue<string>(), "-v", StringComparison.Ordinal));
        Assert.Contains("mark all project files", versionOption["description"]?.GetValue<string>(), StringComparison.Ordinal);

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("ok", metadata["status"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Rejects_CommandLineParser_Runtime_Failure_Output()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "benchmarkdotnet.tool", "0.12.1");
        WriteMetadata(versionRoot, "BenchmarkDotNet.Tool", "0.12.1", "dotnet-benchmark");
        WriteCrawl(versionRoot,
            """
            You must install or update .NET to run this application.

            App: /tmp/inspectra-help-benchmarkdotnet.tool/tool/dotnet-benchmark
            Architecture: x64
            Framework: 'Microsoft.NETCore.App', version '2.1.0' (x64)
            .NET location: /usr/share/dotnet

            Learn more:
            https://aka.ms/dotnet/app-launch-failed
            """);

        var regenerator = new ToolHelpCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.False(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("partial", metadata["status"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    private static void WriteMetadata(string versionRoot, string packageId, string version, string command, bool rejectedHelpArtifact = false)
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
