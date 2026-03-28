using System.Text.Json.Nodes;
using System.Xml.Linq;
using Xunit;

public sealed class XmldocOpenCliArtifactRegeneratorTests
{
    [Fact]
    public void Regenerates_Synthesized_OpenCli_From_Stored_Xmldoc()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        RepositoryPathResolver.WriteTextFile(
            Path.Combine(versionRoot, "xmldoc.xml"),
            """
            <Model>
              <Command Name="__default_command">
                <Parameters>
                  <Option Long="verbose">
                    <Description>Verbose output</Description>
                  </Option>
                </Parameters>
              </Command>
            </Model>
            """);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["command"] = "sample",
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    ["opencliSource"] = "synthesized-from-xmldoc",
                    ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "stale",
                    ["version"] = "1.0",
                },
            });

        var regenerator = new XmldocOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.UnchangedCount);
        Assert.Equal(0, result.FailedCount);

        var regenerated = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.Equal("sample", regenerated["info"]?["title"]?.GetValue<string>());
        Assert.Equal("1.2.3", regenerated["info"]?["version"]?.GetValue<string>());
        Assert.Contains(regenerated["options"]!.AsArray(), option =>
            string.Equals(option?["name"]?.GetValue<string>(), "--verbose", StringComparison.Ordinal));
    }

    [Fact]
    public void Ignores_NonSynthesized_OpenCli_Artifacts()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        RepositoryPathResolver.WriteTextFile(Path.Combine(versionRoot, "xmldoc.xml"), "<Model />");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["command"] = "sample",
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    ["opencliSource"] = "tool-output",
                    ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "native",
                    ["version"] = "1.0",
                },
            });

        var regenerator = new XmldocOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(0, result.CandidateCount);
        Assert.Equal(0, result.RewrittenCount);
        Assert.Equal("native", ParseJsonObject(Path.Combine(versionRoot, "opencli.json"))["info"]?["title"]?.GetValue<string>());
    }

    [Fact]
    public void Repairs_Stale_Xmldoc_Metadata_When_OpenCli_Is_Already_Current()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var xmlDocPath = Path.Combine(versionRoot, "xmldoc.xml");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        RepositoryPathResolver.WriteTextFile(
            xmlDocPath,
            """
            <Model>
              <Command Name="__default_command">
                <Parameters>
                  <Option Long="verbose">
                    <Description>Verbose output</Description>
                  </Option>
                </Parameters>
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
                ["status"] = "partial",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = "tool-output",
                    },
                },
                ["introspection"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["classification"] = "json-ready",
                        ["artifactSource"] = "tool-output",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sample.tool/1.2.3/metadata.json",
                    ["opencliPath"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    ["opencliSource"] = "tool-output",
                    ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            OpenCliDocumentSynthesizer.ConvertFromXmldoc(
                XDocument.Parse(File.ReadAllText(xmlDocPath)),
                "sample",
                "1.2.3"));

        var regenerator = new XmldocOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("ok", metadata["status"]?.GetValue<string>());
        Assert.Equal("synthesized-from-xmldoc", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/1.2.3/opencli.json", metadata["steps"]?["opencli"]?["path"]?.GetValue<string>());
        Assert.Equal("synthesized-from-xmldoc", metadata["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>());
        Assert.Equal("synthesized-from-xmldoc", metadata["introspection"]?["opencli"]?["artifactSource"]?.GetValue<string>());
        Assert.True(metadata["introspection"]?["opencli"]?["synthesizedArtifact"]?.GetValue<bool>());
    }

    [Fact]
    public void Backfills_Missing_OpenCli_From_Stored_Xmldoc_Metadata()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        RepositoryPathResolver.WriteTextFile(
            Path.Combine(versionRoot, "xmldoc.xml"),
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
                ["status"] = "failed",
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sample.tool/1.2.3/metadata.json",
                    ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                },
            });

        var regenerator = new XmldocOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.True(File.Exists(openCliPath));

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("ok", metadata["status"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/1.2.3/opencli.json", metadata["artifacts"]?["opencliPath"]?.GetValue<string>());
        Assert.Equal("synthesized-from-xmldoc", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/1.2.3/opencli.json", metadata["steps"]?["opencli"]?["path"]?.GetValue<string>());
        Assert.Equal("synthesized-from-xmldoc", metadata["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>());
        Assert.True(metadata["introspection"]?["opencli"]?["synthesizedArtifact"]?.GetValue<bool>());
    }

    [Fact]
    public void Does_Not_Backfill_Over_Existing_Native_OpenCli_When_Metadata_Path_Is_Missing()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        RepositoryPathResolver.WriteTextFile(Path.Combine(versionRoot, "xmldoc.xml"), "<Model />");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["command"] = "sample",
                ["artifacts"] = new JsonObject
                {
                    ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "native",
                    ["version"] = "1.0.0",
                },
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "tool-output",
                },
                ["commands"] = new JsonArray(),
            });

        var regenerator = new XmldocOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(0, result.CandidateCount);
        Assert.Equal("native", ParseJsonObject(openCliPath)["info"]?["title"]?.GetValue<string>());
    }

    [Fact]
    public void Repairs_Corrupted_Synthesized_OpenCli()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        RepositoryPathResolver.WriteTextFile(
            Path.Combine(versionRoot, "xmldoc.xml"),
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
                ["status"] = "partial",
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sample.tool/1.2.3/metadata.json",
                    ["opencliPath"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    ["opencliSource"] = "synthesized-from-xmldoc",
                    ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                },
            });
        RepositoryPathResolver.WriteTextFile(openCliPath, "{not valid json");

        var regenerator = new XmldocOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal("sample", ParseJsonObject(openCliPath)["info"]?["title"]?.GetValue<string>());
        Assert.Equal("synthesized-from-xmldoc", ParseJsonObject(metadataPath)["artifacts"]?["opencliSource"]?.GetValue<string>());
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
