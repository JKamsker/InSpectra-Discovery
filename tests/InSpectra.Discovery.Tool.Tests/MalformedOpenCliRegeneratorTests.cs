namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.CliFx.Artifacts;
using InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;
using InSpectra.Discovery.Tool.Help.Artifacts;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;
using Xunit;

public sealed class MalformedOpenCliRegeneratorTests
{
    [Fact]
    public void HelpRegenerator_Rewrites_Malformed_OpenCli_Using_Metadata_Provenance()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = InitializeRepository(tempDirectory.Path);
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "help.tool", "1.0.0");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Help.Tool",
                ["version"] = "1.0.0",
                ["command"] = "help-tool",
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/help.tool/1.0.0/opencli.json",
                    ["opencliSource"] = "crawled-from-help",
                    ["crawlPath"] = "index/packages/help.tool/1.0.0/crawl.json",
                },
            });
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
                            help-tool

                            Usage: help-tool [--verbose]

                            Options:
                              --verbose  Verbose output.
                            """,
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(openCliPath, new JsonArray { "broken" });

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal("help-tool", ParseJsonObject(openCliPath)["info"]?["title"]?.GetValue<string>());
    }

    [Fact]
    public void CliFxRegenerator_Rewrites_Malformed_OpenCli_Using_Metadata_Provenance()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = InitializeRepository(tempDirectory.Path);
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "clifx.tool", "1.0.0");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "CliFx.Tool",
                ["version"] = "1.0.0",
                ["command"] = "clifx-tool",
                ["cliFramework"] = "CliFx",
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/clifx.tool/1.0.0/opencli.json",
                    ["opencliSource"] = "crawled-from-clifx-help",
                    ["crawlPath"] = "index/packages/clifx.tool/1.0.0/crawl.json",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "crawl.json"),
            new JsonObject
            {
                ["documentCount"] = 1,
                ["captureCount"] = 1,
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = null,
                        ["payload"] =
                            """
                            clifx-tool 1.0.0

                            DESCRIPTION
                              Demo CLI
                            """,
                    },
                },
                ["staticCommands"] = CliFxCrawlArtifactSupport.SerializeStaticCommands(
                    new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)),
            });
        RepositoryPathResolver.WriteJsonFile(openCliPath, new JsonArray { "broken" });

        var regenerator = new CliFxCrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal("clifx-tool", ParseJsonObject(openCliPath)["info"]?["title"]?.GetValue<string>());
    }

    [Fact]
    public void XmldocRegenerator_Rewrites_Malformed_OpenCli_When_Metadata_Leaves_Provenance_Blank()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = InitializeRepository(tempDirectory.Path);
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "xmldoc.tool", "1.0.0");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Xmldoc.Tool",
                ["version"] = "1.0.0",
                ["command"] = "xmldoc-tool",
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/xmldoc.tool/1.0.0/opencli.json",
                    ["xmldocPath"] = "index/packages/xmldoc.tool/1.0.0/xmldoc.xml",
                },
            });
        RepositoryPathResolver.WriteTextFile(
            Path.Combine(versionRoot, "xmldoc.xml"),
            """
            <Model>
              <Command Name="__default_command">
                <Description>XML documentation</Description>
              </Command>
            </Model>
            """);
        RepositoryPathResolver.WriteJsonFile(openCliPath, new JsonArray { "broken" });

        var regenerator = new XmldocOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal("xmldoc-tool", ParseJsonObject(openCliPath)["info"]?["title"]?.GetValue<string>());
    }

    [Fact]
    public void NativeRegenerator_Rejects_Malformed_OpenCli_Without_Aborting_The_Run()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = InitializeRepository(tempDirectory.Path);
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "native.tool", "1.0.0");

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Native.Tool",
                ["version"] = "1.0.0",
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/native.tool/1.0.0/opencli.json",
                    ["opencliSource"] = "tool-output",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonArray { "broken" });

        var regenerator = new NativeOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Null(metadata["artifacts"]?["opencliPath"]);
        Assert.Null(metadata["artifacts"]?["opencliSource"]);
        Assert.Equal("invalid-opencli-artifact", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    private static string InitializeRepository(string root)
    {
        RepositoryPathResolver.WriteTextFile(Path.Combine(root, "InSpectra.Discovery.sln"), string.Empty);
        return root;
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"inspectra-tests-{Guid.NewGuid():N}");
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


