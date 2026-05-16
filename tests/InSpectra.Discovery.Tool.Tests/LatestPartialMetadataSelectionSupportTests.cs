namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Docs.Services;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class LatestPartialMetadataSelectionSupportTests
{
    [Fact]
    public void Select_UsesTopLevelAnalysisModeWhenStepModeIsMissing()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        WriteLatestMetadata(
            tempDirectory.Path,
            packageId: "Apizr.Tools.NSwag",
            version: "5.4.0",
            attempt: 2,
            status: "partial",
            analysisMode: "help",
            stepAnalysisMode: null,
            classification: "invalid-opencli-artifact",
            message: "OpenCLI artifact does not expose any commands, options, or arguments.");

        var items = LatestPartialMetadataSelectionSupport.Select(
            tempDirectory.Path,
            new LatestPartialMetadataSelectionCriteria(AnalysisMode: "help"));

        var item = Assert.Single(items);
        Assert.Equal("Apizr.Tools.NSwag", item.PackageId);
        Assert.Equal("help", item.AnalysisMode);
        Assert.Equal(3, item.NextAttempt);
    }

    [Fact]
    public void Select_FiltersByMessageSubstringAndLimit()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        WriteLatestMetadata(
            tempDirectory.Path,
            packageId: "A.Tool",
            version: "1.0.0",
            attempt: 1,
            status: "partial",
            analysisMode: "static",
            stepAnalysisMode: "static",
            classification: "invalid-opencli-artifact",
            message: "OpenCLI artifact has a non-publishable 'info.title' value.");
        WriteLatestMetadata(
            tempDirectory.Path,
            packageId: "B.Tool",
            version: "1.0.0",
            attempt: 4,
            status: "partial",
            analysisMode: "static",
            stepAnalysisMode: "static",
            classification: "invalid-opencli-artifact",
            message: "OpenCLI artifact has a non-publishable 'info.title' value.");
        WriteLatestMetadata(
            tempDirectory.Path,
            packageId: "C.Tool",
            version: "1.0.0",
            attempt: 1,
            status: "ok",
            analysisMode: "static",
            stepAnalysisMode: "static",
            classification: "static-crawl",
            message: null);

        var items = LatestPartialMetadataSelectionSupport.Select(
            tempDirectory.Path,
            new LatestPartialMetadataSelectionCriteria(
                AnalysisMode: "static",
                MessageContains: "info.title",
                Limit: 1));

        var item = Assert.Single(items);
        Assert.Equal("A.Tool", item.PackageId);
        Assert.Equal(2, item.NextAttempt);
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
            ["artifacts"] = new JsonObject
            {
                ["metadataPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, Path.Combine(latestRoot, "metadata.json")),
            },
        };

        RepositoryPathResolver.WriteJsonFile(Path.Combine(latestRoot, "metadata.json"), metadata);
    }
}
