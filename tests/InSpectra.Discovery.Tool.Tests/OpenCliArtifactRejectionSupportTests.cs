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
}
