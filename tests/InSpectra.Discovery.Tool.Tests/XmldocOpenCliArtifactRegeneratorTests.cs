using System.Text.Json.Nodes;
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
