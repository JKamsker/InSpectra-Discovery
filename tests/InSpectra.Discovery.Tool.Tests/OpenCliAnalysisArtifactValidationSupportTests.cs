namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.NonSpectre;
using InSpectra.Discovery.Tool.Analysis.OpenCli;

using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliAnalysisArtifactValidationSupportTests
{
    [Fact]
    public void TryWriteValidatedArtifact_Rejects_Invalid_OpenCli_Documents_Before_Applying_Success()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var result = NonSpectreResultSupport.CreateInitialResult(
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            batchId: "batch-001",
            attempt: 1,
            source: "unit-test",
            cliFramework: "CliFx",
            analysisMode: "clifx",
            analyzedAt: DateTimeOffset.Parse("2026-04-02T00:00:00Z"));
        var openCliDocument = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["commands"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "__default_command",
                },
            },
        };

        var written = OpenCliAnalysisArtifactValidationSupport.TryWriteValidatedArtifact(
            result,
            tempDirectory.Path,
            openCliDocument,
            successClassification: "clifx-crawl",
            artifactSource: "crawled-from-clifx-help");

        Assert.False(written);
        Assert.Equal("terminal-failure", result["disposition"]?.GetValue<string>());
        Assert.Equal("opencli", result["phase"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", result["classification"]?.GetValue<string>());
        Assert.Equal(
            "OpenCLI artifact contains a '__default_command' node at '$.commands[0]'.",
            result["failureMessage"]?.GetValue<string>());
        Assert.Null(result["artifacts"]?["opencliArtifact"]?.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, "opencli.json")));
    }
}
