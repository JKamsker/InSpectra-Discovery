namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliDocumentSanitizerTests
{
    [Fact]
    public void ApplyNuGetMetadata_Ignores_NonPublishable_NuGet_Title()
    {
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "weixin",
                ["version"] = "0.1.1",
            },
        };

        OpenCliDocumentSanitizer.ApplyNuGetMetadata(document, "Senparc.WebSocket.dll", null);

        Assert.Equal("weixin", document["info"]?["title"]?.GetValue<string>());
    }

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
    public void Sanitize_Does_Not_Merge_Informational_Option_With_Value_Taking_Duplicate()
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
                    ["description"] = "Package version to publish.",
                    ["arguments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "VERSION",
                            ["required"] = true,
                            ["arity"] = new JsonObject
                            {
                                ["minimum"] = 1,
                                ["maximum"] = 1,
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["name"] = "--version",
                    ["description"] = "Display version information.",
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var options = document["options"]!.AsArray();
        Assert.Equal(2, options.Count);
        Assert.Contains(options, option => option?["arguments"] is JsonArray);
        Assert.Contains(options, option => string.Equals(option?["description"]?.GetValue<string>(), "Display version information.", StringComparison.Ordinal));
    }

    [Fact]
    public void Sanitize_Merges_Informational_Option_With_Synthetic_Self_Argument_Duplicate()
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
                    ["arguments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "VERSION",
                            ["required"] = false,
                            ["arity"] = new JsonObject
                            {
                                ["minimum"] = 0,
                                ["maximum"] = 1,
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["name"] = "--version",
                    ["description"] = "Display version information.",
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var version = Assert.Single(document["options"]!.AsArray());
        Assert.Equal("--version", version!["name"]?.GetValue<string>());
        Assert.Equal("Display version information.", version["description"]?.GetValue<string>());
        Assert.Null(version["arguments"]);
    }

    [Fact]
    public void Sanitize_Merges_WellKnown_Informational_Option_By_Name_When_Descriptions_Drift()
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
                    ["description"] = "Display Retype information",
                    ["aliases"] = new JsonArray("-v"),
                },
                new JsonObject
                {
                    ["name"] = "--version",
                    ["description"] = "Retype build information",
                    ["arguments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["required"] = false,
                            ["arity"] = new JsonObject
                            {
                                ["minimum"] = 0,
                                ["maximum"] = 1,
                            },
                            ["type"] = "Boolean",
                        },
                    },
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var version = Assert.Single(document["options"]!.AsArray())!;
        Assert.Equal("--version", version["name"]?.GetValue<string>());
        Assert.Equal("Display Retype information", version["description"]?.GetValue<string>());
        Assert.Equal("-v", Assert.Single(version["aliases"]!.AsArray())!.GetValue<string>());
        Assert.Null(version["arguments"]);
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

    [Fact]
    public void Sanitize_Merges_Standalone_Alias_Into_Richer_Option()
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
                    ["name"] = "-c",
                },
                new JsonObject
                {
                    ["name"] = "--config",
                    ["aliases"] = new JsonArray("-c"),
                    ["description"] = "Configuration file to load.",
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var config = Assert.Single(document["options"]!.AsArray());
        Assert.Equal("--config", config!["name"]?.GetValue<string>());
        Assert.Equal("Configuration file to load.", config["description"]?.GetValue<string>());
        Assert.Contains(config["aliases"]!.AsArray(), alias => string.Equals(alias?.GetValue<string>(), "-c", StringComparison.Ordinal));
    }

    [Fact]
    public void Sanitize_Merges_Same_Option_With_Alternative_Value_Forms()
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
                    ["name"] = "--regex",
                    ["description"] = "Read regex patterns from FILE (one per line)",
                    ["arguments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "FILE",
                            ["required"] = true,
                            ["arity"] = new JsonObject
                            {
                                ["minimum"] = 1,
                                ["maximum"] = 1,
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["name"] = "--regex",
                    ["description"] = "Regex pattern(s) that should match (can specify multiple)",
                    ["arguments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "PATTERN",
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
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var regex = Assert.Single(document["options"]!.AsArray())!.AsObject();
        Assert.Equal("--regex", regex["name"]?.GetValue<string>());
        Assert.Equal("REGEX", regex["arguments"]![0]!["name"]?.GetValue<string>());
        Assert.Contains("Read regex patterns from FILE", regex["description"]?.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("Regex pattern(s) that should match", regex["description"]?.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_Does_Not_Merge_Different_Informational_Options_That_Only_Share_A_Primary_Name()
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
                    ["name"] = "--help",
                    ["aliases"] = new JsonArray("-h"),
                    ["description"] = "Show help information.",
                },
                new JsonObject
                {
                    ["name"] = "--help",
                    ["aliases"] = new JsonArray("/?"),
                    ["description"] = "Show help information.",
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        Assert.Equal(2, document["options"]!.AsArray().Count);
    }

    [Fact]
    public void Sanitize_Merges_Localized_Informational_Option_With_Optional_Boolean_Duplicate()
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
                    ["description"] = "Versionsinformationen anzeigen",
                },
                new JsonObject
                {
                    ["name"] = "--version",
                    ["description"] = "Show version information.",
                    ["aliases"] = new JsonArray("-v"),
                    ["arguments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["required"] = false,
                            ["arity"] = new JsonObject
                            {
                                ["minimum"] = 0,
                                ["maximum"] = 1,
                            },
                            ["type"] = "Boolean",
                        },
                    },
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var version = Assert.Single(document["options"]!.AsArray())!.AsObject();
        Assert.Equal("--version", version["name"]?.GetValue<string>());
        Assert.Equal("Versionsinformationen anzeigen", version["description"]?.GetValue<string>());
        Assert.Equal("-v", Assert.Single(version["aliases"]!.AsArray())!.GetValue<string>());
        Assert.False(version.ContainsKey("arguments"));
    }

    [Fact]
    public void Sanitize_Promotes_Derived_Long_Name_When_Duplicate_Primary_Option_Names_Collide()
    {
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "demo",
                ["version"] = "1.0.0",
            },
            ["commands"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "g2c",
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "--output",
                            ["aliases"] = new JsonArray("-o"),
                            ["description"] = "The implementation file to create.",
                            ["arguments"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["name"] = "OUTPUT-FILENAME",
                                    ["required"] = true,
                                },
                            },
                        },
                        new JsonObject
                        {
                            ["name"] = "--output",
                            ["aliases"] = new JsonArray("-on"),
                            ["description"] = "The target namespace for the output.",
                            ["arguments"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["name"] = "OUTPUT-NAMESPACE",
                                    ["required"] = true,
                                },
                            },
                        },
                    },
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var options = document["commands"]![0]!["options"]!.AsArray();
        Assert.Equal("--output", options[0]!["name"]?.GetValue<string>());
        Assert.Equal("--output-namespace", options[1]!["name"]?.GetValue<string>());
        Assert.Equal("-on", Assert.Single(options[1]!["aliases"]!.AsArray())!.GetValue<string>());
    }

    [Fact]
    public void Sanitize_Does_Not_Hide_WellKnown_Informational_Primary_Name_Collisions()
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
                    ["aliases"] = new JsonArray("-v"),
                    ["description"] = "Will cause the application to mark all project files with this version number",
                },
                new JsonObject
                {
                    ["name"] = "--version",
                    ["description"] = "Display version information.",
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var options = document["options"]!.AsArray();
        Assert.Equal(2, options.Count);
        Assert.All(
            options.OfType<JsonObject>(),
            option => Assert.Equal("--version", option["name"]?.GetValue<string>()));
    }

    [Fact]
    public void Sanitize_Does_Not_Merge_Custom_Version_Self_Argument_With_BuiltIn_Version_Row()
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
                    ["aliases"] = new JsonArray("-v"),
                    ["description"] = "Will cause the application to mark all project files with this version number",
                    ["arguments"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "VERSION",
                            ["required"] = false,
                            ["arity"] = new JsonObject
                            {
                                ["minimum"] = 0,
                                ["maximum"] = 1,
                            },
                        },
                    },
                },
                new JsonObject
                {
                    ["name"] = "--version",
                    ["description"] = "Display version information.",
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var options = document["options"]!.AsArray();
        Assert.Equal(2, options.Count);
        Assert.Contains(
            options.OfType<JsonObject>(),
            option => option["arguments"] is JsonArray);
        Assert.Contains(
            options.OfType<JsonObject>(),
            option => string.Equals(option["description"]?.GetValue<string>(), "Display version information.", StringComparison.Ordinal));
    }
}
