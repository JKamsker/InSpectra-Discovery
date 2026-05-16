namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;
using Xunit;

public sealed class NativeOpenCliArtifactLegacyProvenanceTests
{
    [Fact]
    public void Regenerator_Repairs_Legacy_OpenCli_With_Blank_Provenance_When_No_Derived_Artifacts_Exist()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = InitializeRepository(tempDirectory.Path);
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "native.tool", "1.0.0");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");

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
                    ["version"] = "1.0.0",
                },
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--help",
                        ["description"] = "Show help.",
                    },
                },
            });

        var regenerator = new NativeOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);

        var openCli = ParseJsonObject(openCliPath);
        Assert.Equal("tool-output", openCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
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
