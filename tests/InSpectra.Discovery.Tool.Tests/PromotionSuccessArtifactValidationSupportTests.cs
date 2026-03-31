namespace InSpectra.Discovery.Tool.Tests;

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

    private static JsonObject CreatePlanItem(string packageId, string version, string analysisMode)
        => new()
        {
            ["packageId"] = packageId,
            ["version"] = version,
            ["attempt"] = 1,
            ["analysisMode"] = analysisMode,
        };

    private static JsonObject CreateSuccessResult(string packageId, string version)
        => new()
        {
            ["packageId"] = packageId,
            ["version"] = version,
            ["attempt"] = 1,
            ["analysisMode"] = "native",
            ["disposition"] = "success",
            ["command"] = packageId.ToLowerInvariant(),
            ["artifacts"] = new JsonObject
            {
                ["opencliArtifact"] = "opencli.json",
                ["crawlArtifact"] = null,
                ["xmldocArtifact"] = "xmldoc.xml",
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

