namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;
using Xunit;

public sealed class NativeOpenCliArtifactRegeneratorTests
{
    [Fact]
    public void Regenerates_Native_OpenCli_Artifacts_And_Syncs_Metadata()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "native.tool", "1.2.3");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        var xmlDocPath = Path.Combine(versionRoot, "xmldoc.xml");
        RepositoryPathResolver.WriteTextFile(xmlDocPath, "<Model />");
        RepositoryPathResolver.WriteJsonFile(
            metadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Native.Tool",
                ["version"] = "1.2.3",
                ["status"] = "partial",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["status"] = "failed",
                        ["classification"] = "json-ready-with-nonzero-exit",
                        ["message"] = "stale failure",
                    },
                },
                ["introspection"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["status"] = "unsupported",
                        ["classification"] = "json-ready-with-nonzero-exit",
                        ["message"] = "stale introspection failure",
                        ["synthesizedArtifact"] = true,
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/native.tool/1.2.3/metadata.json",
                    ["opencliPath"] = "index/packages/native.tool/1.2.3/opencli.json",
                    ["opencliSource"] = "tool-output",
                    ["xmldocPath"] = "index/packages/native.tool/1.2.3/xmldoc.xml",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "native-tool",
                    ["version"] = "1.2.3",
                },
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--verbose",
                        ["required"] = false,
                    },
                },
            });

        var regenerator = new NativeOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(openCliPath);
        Assert.Equal("tool-output", openCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
        Assert.Null(openCli["options"]?[0]?["required"]);

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("ok", metadata["status"]?.GetValue<string>());
        Assert.Equal("tool-output", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
        Assert.Equal("index/packages/native.tool/1.2.3/xmldoc.xml", metadata["artifacts"]?["xmldocPath"]?.GetValue<string>());
        Assert.Equal("ok", metadata["steps"]?["opencli"]?["status"]?.GetValue<string>());
        Assert.Equal("tool-output", metadata["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>());
        Assert.Equal("json-ready-with-nonzero-exit", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.Null(metadata["steps"]?["opencli"]?["message"]);
        Assert.Equal("index/packages/native.tool/1.2.3/xmldoc.xml", metadata["steps"]?["xmldoc"]?["path"]?.GetValue<string>());
        Assert.Equal("ok", metadata["introspection"]?["opencli"]?["status"]?.GetValue<string>());
        Assert.Equal("json-ready-with-nonzero-exit", metadata["introspection"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.Null(metadata["introspection"]?["opencli"]?["message"]);
        Assert.Null(metadata["introspection"]?["opencli"]?["synthesizedArtifact"]);
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "native.tool", "latest", "opencli.json")));
    }

    [Fact]
    public void Ignores_Help_Derived_OpenCli_Artifacts()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var nativeVersionRoot = Path.Combine(repositoryRoot, "index", "packages", "native.tool", "1.2.3");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(nativeVersionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Native.Tool",
                ["version"] = "1.2.3",
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/native.tool/1.2.3/opencli.json",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(nativeVersionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "native-tool",
                    ["version"] = "1.2.3",
                },
            });

        var helpVersionRoot = Path.Combine(repositoryRoot, "index", "packages", "help.tool", "2.0.0");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(helpVersionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Help.Tool",
                ["version"] = "2.0.0",
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/help.tool/2.0.0/opencli.json",
                    ["crawlPath"] = "index/packages/help.tool/2.0.0/crawl.json",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(helpVersionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-help",
                },
            });

        var regenerator = new NativeOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(2, result.ScannedCount);
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var helpOpenCli = ParseJsonObject(Path.Combine(helpVersionRoot, "opencli.json"));
        Assert.Equal("crawled-from-help", helpOpenCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
    }

    [Fact]
    public void Prefers_OpenCli_Artifact_Provenance_Over_Stale_Metadata()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    ["opencliSource"] = "tool-output",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-help",
                },
                ["commands"] = new JsonArray(),
            });

        var regenerator = new NativeOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(0, result.CandidateCount);
        Assert.Equal("tool-output", ParseJsonObject(Path.Combine(versionRoot, "metadata.json"))["artifacts"]?["opencliSource"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerates_Native_OpenCli_When_Only_Dead_Crawl_And_Xmldoc_Paths_Remain()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "stale.tool", "1.2.3");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            metadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Stale.Tool",
                ["version"] = "1.2.3",
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/stale.tool/1.2.3/opencli.json",
                    ["crawlPath"] = "index/packages/stale.tool/1.2.3/crawl.json",
                    ["xmldocPath"] = "index/packages/stale.tool/1.2.3/xmldoc.xml",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "stale-tool",
                    ["version"] = "1.2.3",
                },
                ["commands"] = new JsonArray(),
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--help",
                        ["description"] = "Show help",
                    },
                },
            });

        var regenerator = new NativeOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("tool-output", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
        Assert.Null(metadata["artifacts"]?["crawlPath"]);
        Assert.Null(metadata["artifacts"]?["xmldocPath"]);
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


