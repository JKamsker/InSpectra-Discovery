namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CrawlArtifactBuilderTests
{
    [Fact]
    public void Build_Includes_Counts_Sorted_Captures_And_Metadata()
    {
        var captures = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
        {
            ["zeta"] = new()
            {
                ["command"] = "zeta",
            },
            ["alpha"] = new()
            {
                ["command"] = "alpha",
            },
        };

        var artifact = CrawlArtifactBuilder.Build(
            documentCount: 1,
            captures,
            new JsonObject
            {
                ["coverage"] = new JsonObject
                {
                    ["status"] = "partial",
                },
            });

        Assert.Equal(1, artifact["documentCount"]?.GetValue<int>());
        Assert.Equal(1, artifact["commandCount"]?.GetValue<int>());
        Assert.Equal(2, artifact["captureCount"]?.GetValue<int>());
        Assert.Equal("partial", artifact["coverage"]?["status"]?.GetValue<string>());

        var commands = artifact["commands"]?.AsArray() ?? [];
        Assert.Equal(2, commands.Count);
        Assert.Equal("alpha", commands[0]?["command"]?.GetValue<string>());
        Assert.Equal("zeta", commands[1]?["command"]?.GetValue<string>());
    }
}

