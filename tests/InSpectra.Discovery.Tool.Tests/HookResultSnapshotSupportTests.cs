namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;

using Xunit;

public sealed class HookResultSnapshotSupportTests
{
    [Fact]
    public void SerializeForComparison_IgnoresVolatileExecutionFields()
    {
        var earlier = CreateResult("2026-04-01T00:00:00Z", "batch-001", 1, "source-a");
        var later = CreateResult("2026-04-01T01:00:00Z", "batch-999", 7, "source-b");

        Assert.Equal(
            HookResultSnapshotSupport.SerializeForComparison(earlier),
            HookResultSnapshotSupport.SerializeForComparison(later));
    }

    private static JsonNode CreateResult(string analyzedAt, string batchId, int attempt, string source)
        => new JsonObject
        {
            ["packageId"] = "METU.CORE",
            ["version"] = "2025.805.1.1",
            ["batchId"] = batchId,
            ["attempt"] = attempt,
            ["source"] = source,
            ["analyzedAt"] = analyzedAt,
            ["disposition"] = "terminal-failure",
            ["retryEligible"] = false,
            ["phase"] = "hook-setup",
            ["classification"] = "hook-invalid-dotnet-entrypoint",
            ["failureMessage"] = "Dotnet tool entry point 'METU.CORE.dll' does not contain a managed entry point.",
            ["failureSignature"] = "hook-setup|hook-invalid-dotnet-entrypoint|Dotnet tool entry point 'METU.CORE.dll' does not contain a managed entry point.",
            ["command"] = "METU.CORE",
            ["cliFramework"] = "Microsoft.Extensions.CommandLineUtils",
            ["analysisMode"] = "hook",
            ["analysisSelection"] = new JsonObject
            {
                ["preferredMode"] = "static",
                ["selectedMode"] = "hook",
                ["reason"] = "confirmed-static-analysis-framework",
            },
        };
}
