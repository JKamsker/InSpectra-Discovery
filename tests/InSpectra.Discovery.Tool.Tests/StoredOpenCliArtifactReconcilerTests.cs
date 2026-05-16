namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;
using Xunit;

public sealed class StoredOpenCliArtifactReconcilerTests
{
    [Fact]
    public void Reconciles_Legacy_Help_Derived_OpenCli_Without_A_Stored_Crawl()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "legacy.help.tool", "1.0.0");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            metadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Legacy.Help.Tool",
                ["version"] = "1.0.0",
                ["status"] = "partial",
                ["command"] = "legacy-help",
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/legacy.help.tool/1.0.0/metadata.json",
                    ["opencliPath"] = "index/packages/legacy.help.tool/1.0.0/opencli.json",
                    ["opencliSource"] = "tool-output",
                },
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["status"] = "failed",
                    },
                },
                ["introspection"] = new JsonObject(),
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "Legacy.Help.Tool",
                    ["version"] = "1.0.0",
                },
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-help",
                    ["generator"] = "InSpectra.Discovery",
                    ["helpDocumentCount"] = 2,
                },
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "serve",
                        ["hidden"] = false,
                    },
                },
            });

        var reconciler = new StoredOpenCliArtifactReconciler();
        var result = reconciler.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("ok", metadata["status"]?.GetValue<string>());
        Assert.Equal("help", metadata["analysisMode"]?.GetValue<string>());
        Assert.Equal("crawled-from-help", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
        Assert.Equal("ok", metadata["steps"]?["opencli"]?["status"]?.GetValue<string>());
        Assert.Equal("help-crawl", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.Equal("help-crawl", metadata["introspection"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "legacy.help.tool", "latest", "opencli.json")));
    }

    [Fact]
    public void Rejects_Legacy_Empty_OpenCli_Artifacts_During_Reconciliation()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "legacy.empty.tool", "2.0.0");
        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            metadataPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Legacy.Empty.Tool",
                ["version"] = "2.0.0",
                ["status"] = "partial",
                ["command"] = "legacy-empty",
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/legacy.empty.tool/2.0.0/metadata.json",
                    ["opencliPath"] = "index/packages/legacy.empty.tool/2.0.0/opencli.json",
                    ["opencliSource"] = "tool-output",
                },
                ["steps"] = new JsonObject(),
                ["introspection"] = new JsonObject(),
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "Legacy.Empty.Tool",
                    ["version"] = "2.0.0",
                },
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-help",
                },
                ["commands"] = new JsonArray(),
            });

        var reconciler = new StoredOpenCliArtifactReconciler();
        var result = reconciler.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.False(File.Exists(openCliPath));

        var metadata = ParseJsonObject(metadataPath);
        Assert.Equal("partial", metadata["status"]?.GetValue<string>());
        Assert.Null(metadata["artifacts"]?["opencliPath"]);
        Assert.Equal("invalid-opencli-artifact", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.Equal("OpenCLI artifact does not expose any commands, options, or arguments.", metadata["steps"]?["opencli"]?["message"]?.GetValue<string>());
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
