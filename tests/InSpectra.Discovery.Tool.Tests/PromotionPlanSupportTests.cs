namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Promotion.Planning;

using System.Text.Json.Nodes;
using Xunit;

public sealed class PromotionPlanSupportTests
{
    [Fact]
    public async Task LoadMergedPlanAsync_Preserves_Distinct_Artifact_Items_For_Same_Package_Version()
    {
        using var tempDirectory = new TemporaryDirectory();
        var downloadRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(downloadRoot, "plan-a", "expected.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["batchId"] = "batch-a",
                ["targetBranch"] = "main",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["version"] = "1.2.3",
                        ["command"] = "sample",
                        ["artifactName"] = "analysis-sample.tool-1.2.3-sample",
                        ["attempt"] = 1,
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(downloadRoot, "plan-b", "expected.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["batchId"] = "batch-b",
                ["targetBranch"] = "main",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["version"] = "1.2.3",
                        ["command"] = "sample-alt",
                        ["artifactName"] = "analysis-sample.tool-1.2.3-sample-alt",
                        ["attempt"] = 1,
                    },
                },
            });

        var merged = await PromotionPlanSupport.LoadMergedPlanAsync(downloadRoot, CancellationToken.None);

        Assert.Equal(2, merged.Items.Count);
        Assert.Contains(merged.Items.OfType<JsonObject>(), item => string.Equals(item["artifactName"]?.GetValue<string>(), "analysis-sample.tool-1.2.3-sample", StringComparison.Ordinal));
        Assert.Contains(merged.Items.OfType<JsonObject>(), item => string.Equals(item["artifactName"]?.GetValue<string>(), "analysis-sample.tool-1.2.3-sample-alt", StringComparison.Ordinal));
    }

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

