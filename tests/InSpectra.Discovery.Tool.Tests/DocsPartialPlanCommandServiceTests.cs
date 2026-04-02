namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Docs.Services;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class DocsPartialPlanCommandServiceTests
{
    [Fact]
    public async Task ExportLatestPartialsPlanAsync_WritesExpectedJsonForSelectedLatestPartials()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        WriteLatestMetadata(
            tempDirectory.Path,
            packageId: "Sample.Tool",
            version: "1.2.3",
            attempt: 2,
            status: "partial",
            analysisMode: "help",
            stepAnalysisMode: "help",
            classification: "invalid-opencli-artifact",
            message: "OpenCLI artifact does not expose any commands, options, or arguments.");

        var outputPath = Path.Combine(tempDirectory.Path, "artifacts", "expected.json");
        var service = new DocsPartialPlanCommandService();

        var exitCode = await service.ExportLatestPartialsPlanAsync(
            tempDirectory.Path,
            new LatestPartialMetadataSelectionCriteria(AnalysisMode: "help"),
            "batch-123",
            outputPath,
            "main",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var expected = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        Assert.Equal("batch-123", expected["batchId"]?.GetValue<string>());
        Assert.Equal("main", expected["targetBranch"]?.GetValue<string>());

        var items = expected["items"]!.AsArray();
        var item = Assert.IsType<JsonObject>(Assert.Single(items));
        Assert.Equal("Sample.Tool", item["packageId"]?.GetValue<string>());
        Assert.Equal("1.2.3", item["version"]?.GetValue<string>());
        Assert.Equal(3, item["attempt"]?.GetValue<int>());
    }

    private static void WriteLatestMetadata(
        string repositoryRoot,
        string packageId,
        string version,
        int attempt,
        string status,
        string? analysisMode,
        string? stepAnalysisMode,
        string? classification,
        string? message)
    {
        var lowerId = packageId.ToLowerInvariant();
        var latestRoot = Path.Combine(repositoryRoot, "index", "packages", lowerId, "latest");
        Directory.CreateDirectory(latestRoot);

        var metadata = new JsonObject
        {
            ["packageId"] = packageId,
            ["version"] = version,
            ["attempt"] = attempt,
            ["status"] = status,
            ["analysisMode"] = analysisMode,
            ["analysisSelection"] = new JsonObject
            {
                ["selectedMode"] = analysisMode,
            },
            ["steps"] = new JsonObject
            {
                ["opencli"] = new JsonObject
                {
                    ["analysisMode"] = stepAnalysisMode,
                    ["classification"] = classification,
                    ["message"] = message,
                },
            },
        };

        RepositoryPathResolver.WriteJsonFile(Path.Combine(latestRoot, "metadata.json"), metadata);
    }
}
