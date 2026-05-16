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
        Assert.Equal("tool-output", metadata["opencliSource"]?.GetValue<string>());
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
        Assert.Equal("synthesized-from-xmldoc", metadata["opencliSource"]?.GetValue<string>());
    }

    [Fact]
    public void SyncMetadata_Refreshes_Latest_OpenCli_For_Current_Version()
    {
        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        var packageRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool");
        var versionRoot = Path.Combine(packageRoot, "1.0.0");
        var latestRoot = Path.Combine(packageRoot, "latest");
        Directory.CreateDirectory(versionRoot);
        Directory.CreateDirectory(latestRoot);

        var versionMetadataPath = Path.Combine(versionRoot, "metadata.json");
        var versionOpenCliPath = Path.Combine(versionRoot, "opencli.json");
        var latestMetadataPath = Path.Combine(latestRoot, "metadata.json");
        var latestOpenCliPath = Path.Combine(latestRoot, "opencli.json");

        RepositoryPathResolver.WriteJsonFile(
            versionMetadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.0.0",
                ["status"] = "partial",
                ["artifacts"] = new JsonObject(),
            });
        RepositoryPathResolver.WriteJsonFile(
            versionOpenCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "fresh-sample",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            latestOpenCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "stale-sample",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            latestMetadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.0.0",
                ["status"] = "ok",
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, latestMetadataPath),
                    ["opencliPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, latestOpenCliPath),
                    ["opencliSource"] = "stale-source",
                },
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["status"] = "ok",
                        ["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, latestOpenCliPath),
                    },
                },
            });

        var changed = OpenCliArtifactMetadataRepair.SyncMetadata(
            repositoryRoot,
            versionMetadataPath,
            versionOpenCliPath,
            "tool-output");

        Assert.True(changed);

        var latestMetadata = ParseJsonObject(latestMetadataPath);
        var latestOpenCli = ParseJsonObject(latestOpenCliPath);

        Assert.Equal("ok", latestMetadata["status"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/latest/opencli.json", latestMetadata["artifacts"]?["opencliPath"]?.GetValue<string>());
        Assert.Equal("tool-output", latestMetadata["artifacts"]?["opencliSource"]?.GetValue<string>());
        Assert.Equal("fresh-sample", latestOpenCli["info"]?["title"]?.GetValue<string>());
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
