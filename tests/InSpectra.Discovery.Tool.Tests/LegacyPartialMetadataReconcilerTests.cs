namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;
using Xunit;

public sealed class LegacyPartialMetadataReconcilerTests
{
    [Fact]
    public void Backfills_Unresolved_OpenCli_Failure_For_Legacy_Partials_With_No_Artifacts()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "legacy.partial.tool", "1.0.0");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        RepositoryPathResolver.WriteJsonFile(
            metadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Legacy.Partial.Tool",
                ["version"] = "1.0.0",
                ["status"] = "partial",
                ["command"] = "legacy-partial",
                ["steps"] = new JsonObject
                {
                    ["install"] = new JsonObject
                    {
                        ["status"] = "ok",
                    },
                },
                ["introspection"] = new JsonObject(),
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/legacy.partial.tool/1.0.0/metadata.json",
                },
            });

        var reconciler = new LegacyPartialMetadataReconciler();
        var result = reconciler.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("failed", metadata["steps"]?["opencli"]?["status"]?.GetValue<string>());
        Assert.Equal("introspection-unresolved", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.Equal("The tool did not yield a usable introspection result.", metadata["steps"]?["opencli"]?["message"]?.GetValue<string>());
        Assert.Equal("failed", metadata["introspection"]?["opencli"]?["status"]?.GetValue<string>());
        Assert.Equal("introspection-unresolved", metadata["introspection"]?["opencli"]?["classification"]?.GetValue<string>());
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
