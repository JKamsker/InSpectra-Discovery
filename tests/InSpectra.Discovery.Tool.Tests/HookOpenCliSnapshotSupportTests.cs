namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;

using Xunit;

public sealed class HookOpenCliSnapshotSupportTests
{
    [Fact]
    public void SerializeForComparison_NormalizesDescriptionLineEndings()
    {
        var withCrLf = CreateDocument("Line one\r\nLine two");
        var withLf = CreateDocument("Line one\nLine two");

        Assert.Equal(
            HookOpenCliSnapshotSupport.SerializeForComparison(withCrLf),
            HookOpenCliSnapshotSupport.SerializeForComparison(withLf));
    }

    [Fact]
    public void SerializeForComparison_RemovesVolatileBuildLines()
    {
        var withBuildNoise = CreateDocument(
            "Stable line\r\nBuild started 03/31/2026 17:00:00\r\nBuild 1 succeeded, 0 failed.\r\nTime elapsed 00:00:01.23\r\nTrailing line");
        var withoutBuildNoise = CreateDocument("Stable line\nTrailing line");

        Assert.Equal(
            HookOpenCliSnapshotSupport.SerializeForComparison(withoutBuildNoise),
            HookOpenCliSnapshotSupport.SerializeForComparison(withBuildNoise));
    }

    [Fact]
    public void SerializeForComparison_Canonicalizes_BuiltIn_Option_Descriptions()
    {
        var localized = CreateDocument(
            "Stable line",
            helpDescription: "Hilfemeldung anzeigen",
            versionDescription: "Versionsinformationen anzeigen");
        var canonical = CreateDocument(
            "Stable line",
            helpDescription: "Show help and usage information",
            versionDescription: "Show version information");

        Assert.Equal(
            HookOpenCliSnapshotSupport.SerializeForComparison(canonical),
            HookOpenCliSnapshotSupport.SerializeForComparison(localized));
    }

    private static JsonNode CreateDocument(
        string description,
        string helpDescription = "Show help and usage information",
        string versionDescription = "Show version information")
        => new JsonObject
        {
            ["info"] = new JsonObject
            {
                ["title"] = "Example CLI",
                ["version"] = "1.2.3",
                ["description"] = description,
            },
            ["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--help",
                    ["description"] = helpDescription,
                },
                new JsonObject
                {
                    ["name"] = "--version",
                    ["description"] = versionDescription,
                },
            },
            ["commands"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "sync",
                    ["description"] = description,
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "verbosity",
                            ["description"] = description,
                            ["arguments"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["name"] = "value",
                                    ["description"] = description,
                                    ["type"] = "string",
                                },
                            },
                        },
                    },
                },
            },
        };
}
