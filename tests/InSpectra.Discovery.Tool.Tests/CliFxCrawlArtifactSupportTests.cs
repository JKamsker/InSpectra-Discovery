namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CliFxCrawlArtifactSupportTests
{
    [Fact]
    public void RoundTrips_StaticCommands_With_ValueNames()
    {
        var commands = new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Name: null,
                Description: "Default command",
                Parameters: [],
                Options:
                [
                    new CliFxOptionDefinition(
                        Name: null,
                        ShortName: 's',
                        IsRequired: true,
                        IsSequence: false,
                        IsBoolLike: false,
                        ClrType: "System.String",
                        Description: "Script path",
                        EnvironmentVariable: null,
                        AcceptedValues: [],
                        ValueName: "scriptPath"),
                ]),
        };

        var metadata = CliFxCrawlArtifactSupport.BuildMetadata(
            commands,
            new JsonObject
            {
                ["mode"] = "metadata-augmented",
            });

        var roundTripped = CliFxCrawlArtifactSupport.DeserializeStaticCommands(metadata["staticCommands"]);
        var root = Assert.Single(roundTripped);
        Assert.Equal(string.Empty, root.Key);
        Assert.Equal("scriptPath", Assert.Single(root.Value.Options).ValueName);
        Assert.Equal("metadata-augmented", metadata["coverage"]?["mode"]?.GetValue<string>());
    }
}

