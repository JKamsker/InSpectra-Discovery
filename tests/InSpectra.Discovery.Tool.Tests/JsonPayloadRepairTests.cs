namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class JsonPayloadRepairTests
{
    [Fact]
    public void ExpandCandidates_Repairs_ControlCharacters_Inside_Json_Strings()
    {
        const string malformed = """
            {
              "opencli": "0.1-draft",
              "options": [
                {
                  "name": "--verbosity",
                  "description": "Line one
            Line two	with tab"
                }
              ]
            }
            """;

        Assert.ThrowsAny<Exception>(() => JsonNode.Parse(malformed));

        var candidates = JsonPayloadRepair.ExpandCandidates(malformed).ToArray();

        Assert.Equal(2, candidates.Length);
        var repaired = JsonNode.Parse(candidates[1]);

        Assert.Equal(
            "Line one\nLine two\twith tab",
            repaired?["options"]?[0]?["description"]?.GetValue<string>());
    }

    [Fact]
    public void ExpandCandidates_Leaves_Valid_Json_Unchanged()
    {
        const string valid = """{"opencli":"0.1-draft","description":"already escaped\nvalue"}""";

        var candidates = JsonPayloadRepair.ExpandCandidates(valid).ToArray();

        Assert.Single(candidates);
        Assert.Equal(valid, candidates[0]);
    }
}

