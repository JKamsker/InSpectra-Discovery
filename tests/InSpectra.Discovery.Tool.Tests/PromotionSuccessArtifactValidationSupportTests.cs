namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Artifacts;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Promotion.Artifacts;

using System.Text.Json.Nodes;
using Xunit;

public sealed class PromotionSuccessArtifactValidationSupportTests
{
    [Fact]
    public void Validate_Allows_Xmldoc_Fallback_When_OpenCli_Is_Invalid()
    {
        using var tempDirectory = new PromotionValidationTemporaryDirectory();
        var item = CreatePlanItem("Xmldoc.Tool", "1.2.3", analysisMode: "native");
        var result = CreateSuccessResult("Xmldoc.Tool", "1.2.3");

        File.WriteAllText(Path.Combine(tempDirectory.Path, "opencli.json"), "{invalid json");
        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "xmldoc.xml"),
            """
            <Model>
              <Command Name="__default_command">
                <Description>Sample XML doc</Description>
              </Command>
            </Model>
            """);

        var outcome = PromotionSuccessArtifactValidationSupport.Validate(
            item,
            result,
            tempDirectory.Path,
            batchId: "batch-1",
            now: DateTimeOffset.Parse("2026-03-30T00:00:00Z"));

        Assert.Same(result, outcome.Result);
        Assert.Equal(tempDirectory.Path, outcome.ArtifactDirectory);
        Assert.Equal("success", outcome.Result["disposition"]?.GetValue<string>());
        Assert.Equal("native", result["analysisSelection"]?["selectedMode"]?.GetValue<string>());
    }

    [Fact]
    public void Validate_Returns_Synthetic_Failure_When_Success_Artifacts_Are_Missing()
    {
        using var tempDirectory = new PromotionValidationTemporaryDirectory();
        var item = CreatePlanItem("Broken.Tool", "4.5.6", analysisMode: "native");
        var result = CreateSuccessResult("Broken.Tool", "4.5.6");

        var outcome = PromotionSuccessArtifactValidationSupport.Validate(
            item,
            result,
            tempDirectory.Path,
            batchId: "batch-2",
            now: DateTimeOffset.Parse("2026-03-30T00:00:00Z"));

        Assert.NotSame(result, outcome.Result);
        Assert.Null(outcome.ArtifactDirectory);
        Assert.Equal("retryable-failure", outcome.Result["disposition"]?.GetValue<string>());
        Assert.Equal("missing-success-artifact", outcome.Result["classification"]?.GetValue<string>());
    }

    [Fact]
    public void Validate_Returns_Synthetic_Failure_When_Crawl_Artifact_Exceeds_Limit()
    {
        using var tempDirectory = new PromotionValidationTemporaryDirectory();
        var item = CreatePlanItem("Help.Tool", "2.0.0", analysisMode: "help");
        var result = CreateSuccessResult("Help.Tool", "2.0.0", analysisMode: "help", crawlArtifact: "crawl.json", xmldocArtifact: null);

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(tempDirectory.Path, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "Help Tool",
                    ["version"] = "2.0.0",
                },
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--verbose",
                    },
                },
                ["commands"] = new JsonArray(),
            });

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(tempDirectory.Path, "crawl.json"),
            new JsonObject
            {
                ["documentCount"] = 1,
                ["captureCount"] = 1,
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = "help-tool",
                        ["payload"] = new string('x', CrawlArtifactValidationSupport.MaxArtifactBytes),
                    },
                },
            });

        var outcome = PromotionSuccessArtifactValidationSupport.Validate(
            item,
            result,
            tempDirectory.Path,
            batchId: "batch-3",
            now: DateTimeOffset.Parse("2026-03-30T00:00:00Z"));

        Assert.NotSame(result, outcome.Result);
        Assert.Null(outcome.ArtifactDirectory);
        Assert.Equal("retryable-failure", outcome.Result["disposition"]?.GetValue<string>());
        Assert.Equal("missing-success-artifact", outcome.Result["classification"]?.GetValue<string>());
        Assert.Contains("1 MiB limit", outcome.Result["failureMessage"]?.GetValue<string>());
    }

    private static JsonObject CreatePlanItem(string packageId, string version, string analysisMode)
        => new()
        {
            ["packageId"] = packageId,
            ["version"] = version,
            ["attempt"] = 1,
            ["analysisMode"] = analysisMode,
        };

    private static JsonObject CreateSuccessResult(string packageId, string version, string analysisMode = "native", string? crawlArtifact = null, string? xmldocArtifact = "xmldoc.xml")
        => new()
        {
            ["packageId"] = packageId,
            ["version"] = version,
            ["attempt"] = 1,
            ["analysisMode"] = analysisMode,
            ["disposition"] = "success",
            ["command"] = packageId.ToLowerInvariant(),
            ["artifacts"] = new JsonObject
            {
                ["opencliArtifact"] = "opencli.json",
                ["crawlArtifact"] = crawlArtifact,
                ["xmldocArtifact"] = xmldocArtifact,
            },
        };
}

internal sealed class PromotionValidationTemporaryDirectory : IDisposable
{
    public PromotionValidationTemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"inspectra-promotion-validation-{Guid.NewGuid():N}");
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
