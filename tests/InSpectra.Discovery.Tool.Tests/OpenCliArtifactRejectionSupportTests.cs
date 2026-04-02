namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliArtifactRejectionSupportTests
{
    [Fact]
    public void RejectInvalidArtifact_PreservesResolvedAnalysisModeOnOpenCliStep()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "latest");
        Directory.CreateDirectory(versionRoot);

        var metadataPath = Path.Combine(versionRoot, "metadata.json");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(openCliPath, new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "sample",
            },
        });

        RepositoryPathResolver.WriteJsonFile(metadataPath, new JsonObject
        {
            ["packageId"] = "Sample.Tool",
            ["version"] = "1.2.3",
            ["status"] = "ok",
            ["analysisMode"] = "help",
            ["analysisSelection"] = new JsonObject
            {
                ["selectedMode"] = "help",
            },
            ["steps"] = new JsonObject
            {
                ["opencli"] = new JsonObject
                {
                    ["status"] = "ok",
                },
            },
            ["artifacts"] = new JsonObject
            {
                ["metadataPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, metadataPath),
                ["opencliPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, openCliPath),
                ["opencliSource"] = "crawled-from-help",
            },
        });

        var changed = OpenCliArtifactRejectionSupport.RejectInvalidArtifact(
            repositoryRoot,
            metadataPath,
            openCliPath,
            "Generated OpenCLI artifact is not publishable.");

        Assert.True(changed);
        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))!.AsObject();
        Assert.Equal("help", metadata["steps"]?["opencli"]?["analysisMode"]?.GetValue<string>());
    }

    [Fact]
    public void RejectInvalidArtifact_Deletes_Stale_Latest_OpenCli_For_Current_Version()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        var packageRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool");
        var versionRoot = Path.Combine(packageRoot, "1.2.3");
        var latestRoot = Path.Combine(packageRoot, "latest");
        Directory.CreateDirectory(versionRoot);
        Directory.CreateDirectory(latestRoot);

        var versionMetadataPath = Path.Combine(versionRoot, "metadata.json");
        var versionOpenCliPath = Path.Combine(versionRoot, "opencli.json");
        var latestMetadataPath = Path.Combine(latestRoot, "metadata.json");
        var latestOpenCliPath = Path.Combine(latestRoot, "opencli.json");

        RepositoryPathResolver.WriteJsonFile(versionOpenCliPath, new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "sample",
            },
        });
        RepositoryPathResolver.WriteJsonFile(latestOpenCliPath, new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "stale-sample",
            },
        });

        var versionMetadata = new JsonObject
        {
            ["packageId"] = "Sample.Tool",
            ["version"] = "1.2.3",
            ["status"] = "ok",
            ["analysisMode"] = "help",
            ["analysisSelection"] = new JsonObject
            {
                ["selectedMode"] = "help",
            },
            ["steps"] = new JsonObject
            {
                ["opencli"] = new JsonObject
                {
                    ["status"] = "ok",
                    ["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, versionOpenCliPath),
                },
            },
            ["artifacts"] = new JsonObject
            {
                ["metadataPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, versionMetadataPath),
                ["opencliPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, versionOpenCliPath),
                ["opencliSource"] = "crawled-from-help",
            },
        };

        RepositoryPathResolver.WriteJsonFile(versionMetadataPath, versionMetadata);
        var latestMetadata = versionMetadata.DeepClone().AsObject();
        latestMetadata["artifacts"] = new JsonObject
        {
            ["metadataPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, latestMetadataPath),
            ["opencliPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, latestOpenCliPath),
            ["opencliSource"] = "crawled-from-help",
        };
        RepositoryPathResolver.WriteJsonFile(
            latestMetadataPath,
            latestMetadata);

        var changed = OpenCliArtifactRejectionSupport.RejectInvalidArtifact(
            repositoryRoot,
            versionMetadataPath,
            versionOpenCliPath,
            "Generated OpenCLI artifact is not publishable.");

        Assert.True(changed);
        Assert.False(File.Exists(versionOpenCliPath));
        Assert.False(File.Exists(latestOpenCliPath));

        latestMetadata = JsonNode.Parse(File.ReadAllText(latestMetadataPath))!.AsObject();
        Assert.Equal("partial", latestMetadata["status"]?.GetValue<string>());
        Assert.Null(latestMetadata["artifacts"]?["opencliPath"]);
    }
}
