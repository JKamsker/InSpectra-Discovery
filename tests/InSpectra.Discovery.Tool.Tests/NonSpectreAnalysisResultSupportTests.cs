using System.Text.Json.Nodes;
using Xunit;

public sealed class NonSpectreAnalysisResultSupportTests
{
    [Fact]
    public void CreateInitialResult_Seeds_NonSpectre_Result_Shape()
    {
        var analyzedAt = DateTimeOffset.Parse("2026-03-28T12:00:00Z");

        var result = NonSpectreAnalysisResultSupport.CreateInitialResult(
            "Sample.Tool",
            "1.2.3",
            "sample",
            "batch-001",
            2,
            "help-index-batch",
            "System.CommandLine",
            "help",
            analyzedAt);

        Assert.Equal("help", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("System.CommandLine", result["cliFramework"]?.GetValue<string>());
        Assert.Equal("retryable-failure", result["disposition"]?.GetValue<string>());
        Assert.True(result["retryEligible"]?.GetValue<bool>());
        Assert.Equal("bootstrap", result["phase"]?.GetValue<string>());
        Assert.Equal("uninitialized", result["classification"]?.GetValue<string>());
        Assert.Null(result["failureSignature"]);
        Assert.Null(result["steps"]?["opencli"]);
        Assert.Null(result["introspection"]?["opencli"]);
        Assert.Null(result["artifacts"]?["crawlArtifact"]);
        Assert.Null(result["timings"]?["crawlMs"]);
    }

    [Fact]
    public void ApplyRetryableFailure_Finalizes_Failure_Signature()
    {
        var result = NonSpectreAnalysisResultSupport.CreateInitialResult(
            "Sample.Tool",
            "1.2.3",
            "sample",
            "batch-001",
            1,
            "help-index-batch",
            "System.CommandLine",
            "help",
            DateTimeOffset.UtcNow);

        NonSpectreAnalysisResultSupport.ApplyRetryableFailure(
            result,
            phase: "install",
            classification: "install-timeout",
            message: "Tool installation failed.");
        NonSpectreAnalysisResultSupport.FinalizeFailureSignature(result);

        Assert.Equal("retryable-failure", result["disposition"]?.GetValue<string>());
        Assert.True(result["retryEligible"]?.GetValue<bool>());
        Assert.Equal("install", result["phase"]?.GetValue<string>());
        Assert.Equal("install-timeout", result["classification"]?.GetValue<string>());
        Assert.Equal("install|install-timeout|Tool installation failed.", result["failureSignature"]?.GetValue<string>());
    }

    [Fact]
    public void ApplySuccess_Adds_OpenCli_Provenance_Nodes()
    {
        var result = NonSpectreAnalysisResultSupport.CreateInitialResult(
            "CliFx.Tool",
            "2.0.0",
            "clifx-tool",
            "batch-001",
            1,
            "help-index-batch",
            "CliFx",
            "clifx",
            DateTimeOffset.UtcNow);

        NonSpectreAnalysisResultSupport.ApplySuccess(result, "clifx-crawl", "crawled-from-clifx-help");
        NonSpectreAnalysisResultSupport.FinalizeFailureSignature(result);

        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.False(result["retryEligible"]?.GetValue<bool>());
        Assert.Equal("complete", result["phase"]?.GetValue<string>());
        Assert.Equal("clifx-crawl", result["classification"]?.GetValue<string>());
        Assert.Null(result["failureSignature"]);
        Assert.Equal("crawled-from-clifx-help", result["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>());
        Assert.Equal("clifx-crawl", result["steps"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.Equal("ok", result["introspection"]?["opencli"]?["status"]?.GetValue<string>());
    }
}
