using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliDocumentSanitizerTests
{
    [Fact]
    public void Sanitize_Removes_Null_Optional_Fields_Empty_Examples_And_Option_Required()
    {
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "demo",
                ["version"] = "1.0.0",
                ["description"] = null,
            },
            ["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--verbose",
                    ["required"] = false,
                    ["description"] = null,
                    ["aliases"] = new JsonArray(),
                },
                new JsonObject
                {
                    ["name"] = "--count",
                    ["required"] = false,
                    ["arguments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "VALUE",
                            ["required"] = true,
                            ["arity"] = new JsonObject
                            {
                                ["minimum"] = 1,
                                ["maximum"] = 1,
                            },
                        },
                    },
                },
            },
            ["commands"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "serve",
                    ["description"] = null,
                    ["arguments"] = null,
                    ["options"] = new JsonArray(),
                    ["examples"] = new JsonArray(),
                },
            },
        };

        OpenCliDocumentSanitizer.EnsureArtifactSource(document, "tool-output");
        OpenCliDocumentSanitizer.Sanitize(document);

        Assert.Equal("tool-output", document["x-inspectra"]?["artifactSource"]?.GetValue<string>());
        Assert.False(document["info"]!.AsObject().ContainsKey("description"));

        var verbose = document["options"]![0]!.AsObject();
        Assert.False(verbose.ContainsKey("required"));
        Assert.False(verbose.ContainsKey("description"));
        Assert.False(verbose.ContainsKey("aliases"));

        var count = document["options"]![1]!.AsObject();
        Assert.False(count.ContainsKey("required"));
        Assert.True(count["arguments"]![0]!["required"]!.GetValue<bool>());

        var serve = document["commands"]![0]!.AsObject();
        Assert.False(serve.ContainsKey("description"));
        Assert.False(serve.ContainsKey("arguments"));
        Assert.False(serve.ContainsKey("options"));
        Assert.False(serve.ContainsKey("examples"));
    }

    [Fact]
    public void Sanitize_Merges_Same_Option_When_Descriptions_Are_NearEquivalent()
    {
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "demo",
                ["version"] = "1.0.0",
            },
            ["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--input",
                    ["description"] = "Parse the given string as input.",
                    ["aliases"] = new JsonArray("-i"),
                },
                new JsonObject
                {
                    ["name"] = "--input",
                    ["description"] = "Parse input string.",
                    ["aliases"] = new JsonArray("-i"),
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var options = document["options"]!.AsArray();
        var input = Assert.Single(options);
        Assert.Equal("--input", input!["name"]?.GetValue<string>());
        Assert.Contains(input["aliases"]!.AsArray(), alias => string.Equals(alias?.GetValue<string>(), "-i", StringComparison.Ordinal));
    }

    [Fact]
    public void Sanitize_Merges_Informational_Option_When_Trailing_Positional_Noise_Is_Present()
    {
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "demo",
                ["version"] = "1.0.0",
            },
            ["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--version",
                },
                new JsonObject
                {
                    ["name"] = "--version",
                    ["description"] = "Display version information.\nvalue pos. 0",
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var version = Assert.Single(document["options"]!.AsArray());
        Assert.Equal("--version", version!["name"]?.GetValue<string>());
        Assert.Equal("Display version information.", version["description"]?.GetValue<string>());
    }

    [Fact]
    public void Sanitize_Trims_Trailing_Noise_From_Single_Informational_Option()
    {
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "demo",
                ["version"] = "1.0.0",
            },
            ["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--version",
                    ["description"] = "Display version information.\nvalue pos. 0",
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var version = Assert.Single(document["options"]!.AsArray());
        Assert.Equal("--version", version!["name"]?.GetValue<string>());
        Assert.Equal("Display version information.", version["description"]?.GetValue<string>());
    }

    [Fact]
    public void Sanitize_Prefers_Richer_Compatible_Option_Description_When_Merging()
    {
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "demo",
                ["version"] = "1.0.0",
            },
            ["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--config",
                    ["description"] = "JSON file containing XAML Styler settings",
                    ["aliases"] = new JsonArray("-c"),
                },
                new JsonObject
                {
                    ["name"] = "--config",
                    ["description"] = "JSON file containing XAML Styler settings\nconfiguration.",
                    ["aliases"] = new JsonArray("-c"),
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var config = Assert.Single(document["options"]!.AsArray());
        Assert.Equal(
            "JSON file containing XAML Styler settings\nconfiguration.",
            config!["description"]?.GetValue<string>());
    }
}
