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

    [Fact]
    public void SortPackageSummariesForAllIndex_Falls_Back_To_Versioned_OpenCli_When_Latest_Mirror_Is_Missing()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "inspectra-opencli-metrics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryRoot);

        try
        {
            var metadataPath = Path.Combine(repositoryRoot, "index", "packages", "fallback-tool", "latest", "metadata.json");
            Directory.CreateDirectory(Path.GetDirectoryName(metadataPath)!);
            File.WriteAllText(
                metadataPath,
                """
                {
                  "steps": {
                    "opencli": {
                      "path": "index/packages/fallback-tool/1.0.0/opencli.json"
                    }
                  }
                }
                """);

            var openCliPath = Path.Combine(repositoryRoot, "index", "packages", "fallback-tool", "1.0.0", "opencli.json");
            Directory.CreateDirectory(Path.GetDirectoryName(openCliPath)!);
            File.WriteAllText(
                openCliPath,
                """
                {
                  "opencli": "0.1-draft",
                  "commands": [
                    {
                      "name": "serve",
                      "description": "Serve content."
                    }
                  ]
                }
                """);

            var summary = new JsonObject
            {
                ["packageId"] = "Fallback.Tool",
                ["latestPaths"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/fallback-tool/latest/metadata.json",
                    ["opencliPath"] = "index/packages/fallback-tool/latest/opencli.json",
                },
            };

            var sorted = OpenCliMetrics.SortPackageSummariesForAllIndex([summary], repositoryRoot);

            var updated = Assert.Single(sorted);
            Assert.Equal(0, updated["commandGroupCount"]?.GetValue<int>());
            Assert.Equal(1, updated["commandCount"]?.GetValue<int>());
        }
        finally
        {
            Directory.Delete(repositoryRoot, recursive: true);
        }
    }

    [Fact]
    public void SortPackageSummariesForAllIndex_Falls_Back_To_Versioned_OpenCli_When_Latest_Mirror_Is_Invalid()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "inspectra-opencli-metrics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryRoot);

        try
        {
            var latestOpenCliPath = Path.Combine(repositoryRoot, "index", "packages", "fallback-invalid-tool", "latest", "opencli.json");
            Directory.CreateDirectory(Path.GetDirectoryName(latestOpenCliPath)!);
            File.WriteAllText(latestOpenCliPath, "{not valid json");

            var metadataPath = Path.Combine(repositoryRoot, "index", "packages", "fallback-invalid-tool", "latest", "metadata.json");
            File.WriteAllText(
                metadataPath,
                """
                {
                  "steps": {
                    "opencli": {
                      "path": "index/packages/fallback-invalid-tool/1.0.0/opencli.json"
                    }
                  }
                }
                """);

            var versionedOpenCliPath = Path.Combine(repositoryRoot, "index", "packages", "fallback-invalid-tool", "1.0.0", "opencli.json");
            Directory.CreateDirectory(Path.GetDirectoryName(versionedOpenCliPath)!);
            File.WriteAllText(
                versionedOpenCliPath,
                """
                {
                  "opencli": "0.1-draft",
                  "commands": [
                    {
                      "name": "serve",
                      "description": "Serve content."
                    }
                  ]
                }
                """);

            var summary = new JsonObject
            {
                ["packageId"] = "Fallback.Invalid.Tool",
                ["latestPaths"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/fallback-invalid-tool/latest/metadata.json",
                    ["opencliPath"] = "index/packages/fallback-invalid-tool/latest/opencli.json",
                },
            };

            var sorted = OpenCliMetrics.SortPackageSummariesForAllIndex([summary], repositoryRoot);

            var updated = Assert.Single(sorted);
            Assert.Equal(0, updated["commandGroupCount"]?.GetValue<int>());
            Assert.Equal(1, updated["commandCount"]?.GetValue<int>());
        }
        finally
        {
            Directory.Delete(repositoryRoot, recursive: true);
        }
    }
}
