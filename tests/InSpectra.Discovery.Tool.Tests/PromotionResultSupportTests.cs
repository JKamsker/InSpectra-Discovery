namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Promotion.Planning;
using InSpectra.Discovery.Tool.Promotion.State;

using System.Text.Json.Nodes;
using Xunit;

public sealed class PromotionResultSupportTests
{
    [Fact]
    public void UpdateStateRecord_Escalates_Repeated_Retryable_Failures()
    {
        var existingState = new JsonObject
        {
            ["lastFailureSignature"] = "infra|install-failed|boom",
            ["consecutiveFailureCount"] = 2,
            ["firstEvaluatedAt"] = "2026-03-28T00:00:00Z",
        };
        var result = new JsonObject
        {
            ["packageId"] = "Sample.Tool",
            ["version"] = "1.2.3",
            ["batchId"] = "batch-1",
            ["attempt"] = 3,
            ["analyzedAt"] = "2026-03-30T00:00:00Z",
            ["disposition"] = "retryable-failure",
            ["classification"] = "install-failed",
            ["failureSignature"] = "infra|install-failed|boom",
            ["failureMessage"] = "boom",
            ["phase"] = "install",
        };

        var stateRecord = PromotionStateRecordSupport.UpdateStateRecord(
            existingState,
            result,
            indexedPaths: null,
            now: DateTimeOffset.Parse("2026-03-30T01:00:00Z"));

        Assert.Equal("terminal-failure", stateRecord["currentStatus"]?.GetValue<string>());
        Assert.Equal(3, stateRecord["consecutiveFailureCount"]?.GetValue<int>());
        Assert.False(stateRecord["retryEligible"]?.GetValue<bool>() ?? true);
    }

    [Fact]
    public void UpdateStateRecord_Does_Not_Escalate_Missing_Runtime_Failures()
    {
        var existingState = new JsonObject
        {
            ["lastFailureSignature"] = "infra|environment-missing-runtime|runtime missing",
            ["consecutiveFailureCount"] = 4,
        };
        var result = new JsonObject
        {
            ["packageId"] = "Sample.Tool",
            ["version"] = "1.2.3",
            ["batchId"] = "batch-1",
            ["attempt"] = 5,
            ["analyzedAt"] = "2026-03-30T00:00:00Z",
            ["disposition"] = "retryable-failure",
            ["classification"] = "environment-missing-runtime",
            ["failureSignature"] = "infra|environment-missing-runtime|runtime missing",
            ["failureMessage"] = "runtime missing",
            ["phase"] = "install",
        };

        var stateRecord = PromotionStateRecordSupport.UpdateStateRecord(
            existingState,
            result,
            indexedPaths: null,
            now: DateTimeOffset.Parse("2026-03-30T01:00:00Z"));

        Assert.Equal("retryable-failure", stateRecord["currentStatus"]?.GetValue<string>());
        Assert.True(stateRecord["retryEligible"]?.GetValue<bool>() ?? false);
    }

    [Fact]
    public void MergeIntoResult_Populates_Missing_Analysis_Selection_Fields()
    {
        var item = new JsonObject
        {
            ["analysisMode"] = "clifx",
            ["cliFramework"] = "CliFx",
            ["command"] = "sample",
            ["totalDownloads"] = 42,
        };
        var result = new JsonObject
        {
            ["analysisSelection"] = new JsonObject(),
        };

        PromotionPlanItemMergeSupport.MergeIntoResult(item, result);

        Assert.Equal("clifx", result["analysisMode"]?.GetValue<string>());
        Assert.Equal("clifx", result["analysisSelection"]?["selectedMode"]?.GetValue<string>());
        Assert.Equal("clifx", result["analysisSelection"]?["preferredMode"]?.GetValue<string>());
        Assert.Equal("CliFx", result["cliFramework"]?.GetValue<string>());
        Assert.Equal("sample", result["command"]?.GetValue<string>());
        Assert.Equal(42, result["totalDownloads"]?.GetValue<int>());
    }
}

