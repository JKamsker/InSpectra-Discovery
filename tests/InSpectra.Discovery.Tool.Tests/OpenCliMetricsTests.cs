using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliMetricsTests
{
    [Fact]
    public void GetFromDocument_ReturnsEmpty_ForNonObjectRoot()
    {
        var metrics = OpenCliMetrics.GetFromDocument(JsonValue.Create("legacy-opencli"));

        Assert.Equal(OpenCliMetricsResult.Empty, metrics);
    }

    [Fact]
    public void SortPackageSummariesForAllIndex_ToleratesLegacyNonObjectOpenCliDocuments()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "inspectra-opencli-metrics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryRoot);

        try
        {
            var openCliPath = Path.Combine(repositoryRoot, "index", "packages", "legacy-tool", "latest", "opencli.json");
            Directory.CreateDirectory(Path.GetDirectoryName(openCliPath)!);
            File.WriteAllText(openCliPath, "\"legacy-opencli\"");

            var summary = new JsonObject
            {
                ["packageId"] = "Legacy.Tool",
                ["latestPaths"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/legacy-tool/latest/opencli.json",
                },
            };

            var sorted = OpenCliMetrics.SortPackageSummariesForAllIndex([summary], repositoryRoot);

            var updated = Assert.Single(sorted);
            Assert.Equal(0, updated["commandGroupCount"]?.GetValue<int>());
            Assert.Equal(0, updated["commandCount"]?.GetValue<int>());
        }
        finally
        {
            Directory.Delete(repositoryRoot, recursive: true);
        }
    }
}
