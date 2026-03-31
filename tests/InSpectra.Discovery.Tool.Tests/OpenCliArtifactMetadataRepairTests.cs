namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliArtifactMetadataRepairTests
{
    [Fact]
    public void SyncMetadata_Backfills_AnalysisMode_And_Selection_From_ArtifactSource()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.0.0");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");

        RepositoryPathResolver.WriteJsonFile(
            metadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.0.0",
                ["status"] = "partial",
                ["artifacts"] = new JsonObject(),
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
            });

        var changed = OpenCliArtifactMetadataRepair.SyncMetadata(
            repositoryRoot,
            metadataPath,
            openCliPath,
            "tool-output");

        Assert.True(changed);

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("native", metadata["analysisMode"]?.GetValue<string>());
        Assert.Equal("native", metadata["analysisSelection"]?["selectedMode"]?.GetValue<string>());
        Assert.Equal("native", metadata["analysisSelection"]?["preferredMode"]?.GetValue<string>());
    }

    [Fact]
    public void SyncMetadata_Preserves_PreferredMode_When_Backfilling_SelectedMode()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.0.0");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");

        RepositoryPathResolver.WriteJsonFile(
            metadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.0.0",
                ["status"] = "partial",
                ["analysisSelection"] = new JsonObject
                {
                    ["preferredMode"] = "native",
                },
                ["artifacts"] = new JsonObject(),
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
            });

        _ = OpenCliArtifactMetadataRepair.SyncMetadata(
            repositoryRoot,
            metadataPath,
            openCliPath,
            "synthesized-from-xmldoc");

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("xmldoc", metadata["analysisMode"]?.GetValue<string>());
        Assert.Equal("xmldoc", metadata["analysisSelection"]?["selectedMode"]?.GetValue<string>());
        Assert.Equal("native", metadata["analysisSelection"]?["preferredMode"]?.GetValue<string>());
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

