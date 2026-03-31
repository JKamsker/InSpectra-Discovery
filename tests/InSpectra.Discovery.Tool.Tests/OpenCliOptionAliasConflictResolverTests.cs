namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using System.Text.Json.Nodes;

using Xunit;

public sealed class OpenCliOptionAliasConflictResolverTests
{
    [Fact]
    public void Sanitize_Removes_Conflicting_Alias_From_Later_Option()
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
                    ["name"] = "generate",
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "--exclude-deprecated-operations",
                            ["aliases"] = new JsonArray("-e"),
                            ["description"] = "Exclude deprecated operations.",
                        },
                        new JsonObject
                        {
                            ["name"] = "--clsCompliantEnumPrefix",
                            ["aliases"] = new JsonArray("-e"),
                            ["description"] = "Prefix for enums.",
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
                                    ["type"] = "String",
                                },
                            },
                        },
                    },
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var generate = Assert.Single(document["commands"]!.AsArray())!.AsObject();
        var options = generate["options"]!.AsArray();
        Assert.Equal(2, options.Count);

        var excludeDeprecatedOperations = options[0]!.AsObject();
        var enumPrefix = options[1]!.AsObject();

        Assert.Equal("--exclude-deprecated-operations", excludeDeprecatedOperations["name"]?.GetValue<string>());
        Assert.Equal("-e", Assert.Single(excludeDeprecatedOperations["aliases"]!.AsArray())!.GetValue<string>());

        Assert.Equal("--clsCompliantEnumPrefix", enumPrefix["name"]?.GetValue<string>());
        Assert.False(enumPrefix.ContainsKey("aliases"));

        var valid = OpenCliDocumentValidator.TryValidateDocument(document, out var reason);
        Assert.True(valid, reason);
    }

    [Fact]
    public void Sanitize_Removes_Duplicate_Aliases_From_Same_Option()
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
                    ["name"] = "help",
                    ["description"] = "Show help and usage information",
                    ["aliases"] = new JsonArray("h", "h", "?"),
                },
            },
        };

        OpenCliDocumentSanitizer.Sanitize(document);

        var helpOption = Assert.Single(document["options"]!.AsArray())!.AsObject();
        var aliases = helpOption["aliases"]!.AsArray();
        Assert.Equal(2, aliases.Count);
        Assert.Equal("h", aliases[0]!.GetValue<string>());
        Assert.Equal("?", aliases[1]!.GetValue<string>());

        var valid = OpenCliDocumentValidator.TryValidateDocument(document, out var reason);
        Assert.True(valid, reason);
    }
}
