namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json;
using System.Text.Json.Nodes;

using Xunit;

internal static class HookOpenCliSnapshotSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static void AssertMatchesFixture(string packageId, string version, JsonNode? openCli)
    {
        var actual = Normalize(openCli);
        var fixturePath = ResolveFixturePath(packageId, version);
        Assert.True(File.Exists(fixturePath), $"Missing OpenCLI fixture for {packageId} {version}: {fixturePath}");

        var expected = JsonNode.Parse(File.ReadAllText(fixturePath));
        Assert.NotNull(expected);

        Assert.Equal(Serialize(expected!), Serialize(actual));
    }

    private static JsonObject Normalize(JsonNode? openCli)
    {
        if (openCli is not JsonObject document)
        {
            throw new InvalidOperationException("OpenCLI document is missing or invalid.");
        }

        var normalized = new JsonObject
        {
            ["info"] = NormalizeInfo(document["info"] as JsonObject),
        };

        AddArrayIfNotEmpty(normalized, "options", NormalizeOptions(document["options"] as JsonArray));
        AddArrayIfNotEmpty(normalized, "arguments", NormalizeArguments(document["arguments"] as JsonArray));
        AddArrayIfNotEmpty(normalized, "commands", NormalizeCommands(document["commands"] as JsonArray));
        return normalized;
    }

    private static JsonObject NormalizeInfo(JsonObject? info)
    {
        if (info is null)
        {
            throw new InvalidOperationException("OpenCLI info node is missing.");
        }

        var normalized = new JsonObject
        {
            ["title"] = info["title"]?.GetValue<string>(),
            ["version"] = info["version"]?.GetValue<string>(),
        };

        AddStringIfPresent(normalized, "description", info["description"]?.GetValue<string>());
        return normalized;
    }

    private static JsonArray NormalizeCommands(JsonArray? commands)
    {
        var normalized = new JsonArray();
        if (commands is null)
        {
            return normalized;
        }

        foreach (var entry in commands.OfType<JsonObject>())
        {
            var command = new JsonObject
            {
                ["name"] = entry["name"]?.GetValue<string>(),
            };

            AddStringIfPresent(command, "description", entry["description"]?.GetValue<string>());
            AddBooleanIfTrue(command, "hidden", entry["hidden"]?.GetValue<bool>());
            AddArrayIfNotEmpty(command, "aliases", NormalizeStringArray(entry["aliases"] as JsonArray));
            AddArrayIfNotEmpty(command, "options", NormalizeOptions(entry["options"] as JsonArray));
            AddArrayIfNotEmpty(command, "arguments", NormalizeArguments(entry["arguments"] as JsonArray));
            AddArrayIfNotEmpty(command, "commands", NormalizeCommands(entry["commands"] as JsonArray));

            normalized.Add(command);
        }

        return normalized;
    }

    private static JsonArray NormalizeOptions(JsonArray? options)
    {
        var normalized = new JsonArray();
        if (options is null)
        {
            return normalized;
        }

        foreach (var entry in options.OfType<JsonObject>())
        {
            var option = new JsonObject
            {
                ["name"] = entry["name"]?.GetValue<string>(),
            };

            AddStringIfPresent(option, "description", entry["description"]?.GetValue<string>());
            AddBooleanIfTrue(option, "hidden", entry["hidden"]?.GetValue<bool>());
            AddBooleanIfTrue(option, "recursive", entry["recursive"]?.GetValue<bool>());
            AddArrayIfNotEmpty(option, "aliases", NormalizeStringArray(entry["aliases"] as JsonArray));
            AddArrayIfNotEmpty(option, "arguments", NormalizeArguments(entry["arguments"] as JsonArray));

            normalized.Add(option);
        }

        return normalized;
    }

    private static JsonArray NormalizeArguments(JsonArray? arguments)
    {
        var normalized = new JsonArray();
        if (arguments is null)
        {
            return normalized;
        }

        foreach (var entry in arguments.OfType<JsonObject>())
        {
            var argument = new JsonObject
            {
                ["name"] = entry["name"]?.GetValue<string>(),
            };

            AddStringIfPresent(argument, "description", entry["description"]?.GetValue<string>());
            AddBooleanIfTrue(argument, "hidden", entry["hidden"]?.GetValue<bool>());
            AddBooleanIfTrue(argument, "required", entry["required"]?.GetValue<bool>());
            AddStringIfPresent(argument, "type", entry["type"]?.GetValue<string>());

            if (entry["arity"] is JsonObject arity)
            {
                argument["arity"] = new JsonObject
                {
                    ["minimum"] = arity["minimum"]?.GetValue<int>(),
                    ["maximum"] = arity["maximum"]?.GetValue<int>(),
                };
            }

            AddArrayIfNotEmpty(argument, "allowedValues", NormalizeStringArray(entry["allowedValues"] as JsonArray));
            normalized.Add(argument);
        }

        return normalized;
    }

    private static JsonArray NormalizeStringArray(JsonArray? values)
    {
        var normalized = new JsonArray();
        if (values is null)
        {
            return normalized;
        }

        foreach (var value in values.OfType<JsonValue>())
        {
            normalized.Add(value.GetValue<string>());
        }

        return normalized;
    }

    private static void AddStringIfPresent(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[propertyName] = value;
        }
    }

    private static void AddBooleanIfTrue(JsonObject target, string propertyName, bool? value)
    {
        if (value is true)
        {
            target[propertyName] = true;
        }
    }

    private static void AddArrayIfNotEmpty(JsonObject target, string propertyName, JsonArray values)
    {
        if (values.Count > 0)
        {
            target[propertyName] = values;
        }
    }

    private static string ResolveFixturePath(string packageId, string version)
    {
        var repositoryRoot = RepositoryPathResolver.ResolveRepositoryRoot();
        return Path.Combine(
            repositoryRoot,
            "tests",
            "InSpectra.Discovery.Tool.Tests",
            "TestData",
            "HookOpenCliSnapshots",
            $"{NormalizeSegment(packageId)}--{NormalizeSegment(version)}.json");
    }

    private static string NormalizeSegment(string value)
        => string.Concat(value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-'));

    private static string Serialize(JsonNode value)
        => JsonSerializer.Serialize(value, JsonOptions);
}
