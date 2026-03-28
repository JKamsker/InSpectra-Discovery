using System.Text.Json.Nodes;
using System.Xml.Linq;
using Xunit;

public sealed class XmldocRegeneratorConsistencyTests
{
    [Fact]
    public void RegenerateRepository_Refreshes_Latest_Index_And_State_When_Version_Artifacts_Are_Current()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        var latestRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "latest");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        var xmldocPath = Path.Combine(versionRoot, "xmldoc.xml");
        var versionMetadata = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["packageId"] = "Sample.Tool",
            ["version"] = "1.2.3",
            ["status"] = "ok",
            ["steps"] = new JsonObject
            {
                ["opencli"] = new JsonObject
                {
                    ["status"] = "ok",
                    ["classification"] = "xmldoc-synthesized",
                    ["artifactSource"] = "synthesized-from-xmldoc",
                    ["path"] = "index/packages/sample.tool/1.2.3/opencli.json",
                },
            },
            ["introspection"] = new JsonObject
            {
                ["opencli"] = new JsonObject
                {
                    ["status"] = "ok",
                    ["classification"] = "xmldoc-synthesized",
                    ["artifactSource"] = "synthesized-from-xmldoc",
                    ["synthesizedArtifact"] = true,
                },
            },
            ["artifacts"] = new JsonObject
            {
                ["metadataPath"] = "index/packages/sample.tool/1.2.3/metadata.json",
                ["opencliPath"] = "index/packages/sample.tool/1.2.3/opencli.json",
                ["opencliSource"] = "synthesized-from-xmldoc",
                ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
            },
        };
        RepositoryPathResolver.WriteJsonFile(metadataPath, versionMetadata);
        RepositoryPathResolver.WriteTextFile(
            xmldocPath,
            """
            <Model>
              <Command Name="__default_command">
                <Description>Sample XML doc</Description>
              </Command>
            </Model>
            """);
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            OpenCliDocumentSynthesizer.ConvertFromXmldoc(
                XDocument.Parse(File.ReadAllText(xmldocPath)),
                "sample",
                "1.2.3"));

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(latestRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["status"] = "partial",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["status"] = "failed",
                        ["path"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sample.tool/1.2.3/metadata.json",
                    ["opencliPath"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "index.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["latestVersion"] = "1.2.3",
                ["latestStatus"] = "partial",
                ["latestPaths"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sample.tool/latest/metadata.json",
                    ["opencliPath"] = "index/packages/sample.tool/latest/opencli.json",
                },
                ["versions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["version"] = "1.2.3",
                        ["status"] = "partial",
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "packages", "sample.tool", "1.2.3.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["currentStatus"] = "success",
                ["indexedPaths"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sample.tool/1.2.3/metadata.json",
                    ["opencliPath"] = null,
                    ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                },
            });

        var regenerator = new XmldocOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);

        var latestMetadata = ParseJsonObject(Path.Combine(latestRoot, "metadata.json"));
        Assert.Equal("ok", latestMetadata["status"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/latest/opencli.json", latestMetadata["artifacts"]?["opencliPath"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/latest/opencli.json", latestMetadata["steps"]?["opencli"]?["path"]?.GetValue<string>());

        var packageIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "index.json"));
        Assert.Equal("ok", packageIndex["latestStatus"]?.GetValue<string>());

        var state = ParseJsonObject(Path.Combine(repositoryRoot, "state", "packages", "sample.tool", "1.2.3.json"));
        Assert.Equal("index/packages/sample.tool/1.2.3/opencli.json", state["indexedPaths"]?["opencliPath"]?.GetValue<string>());
    }

    [Fact]
    public void RegenerateRepository_Ignores_Stale_Crawl_Path_When_Xmldoc_Is_The_Only_Usable_Source()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        var xmldocPath = Path.Combine(versionRoot, "xmldoc.xml");
        RepositoryPathResolver.WriteTextFile(
            xmldocPath,
            """
            <Model>
              <Command Name="__default_command">
                <Description>Sample XML doc</Description>
              </Command>
            </Model>
            """);
        RepositoryPathResolver.WriteJsonFile(
            metadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["command"] = "sample",
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sample.tool/1.2.3/metadata.json",
                    ["opencliPath"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                    ["crawlPath"] = "index/packages/sample.tool/1.2.3/crawl.json",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "stale",
                    ["version"] = "1.2.3",
                },
                ["commands"] = new JsonArray(),
            });

        var regenerator = new XmldocOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("synthesized-from-xmldoc", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
        Assert.Null(metadata["artifacts"]?["crawlPath"]);
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
