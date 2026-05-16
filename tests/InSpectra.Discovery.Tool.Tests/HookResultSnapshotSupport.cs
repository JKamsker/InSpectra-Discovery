namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json;
using System.Text.Json.Nodes;

using Xunit;

internal static class HookResultSnapshotSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static void AssertMatchesFixture(string packageId, string version, JsonNode? result)
    {
        var actual = Normalize(result);
        var fixturePath = ResolveFixturePath(packageId, version);
        Assert.True(File.Exists(fixturePath), $"Missing result fixture for {packageId} {version}: {fixturePath}");

        var expected = Normalize(JsonNode.Parse(File.ReadAllText(fixturePath)));
        Assert.NotNull(expected);

        Assert.Equal(Serialize(expected), Serialize(actual));
    }

    internal static string SerializeForComparison(JsonNode? result)
        => Serialize(Normalize(result));

    private static JsonObject Normalize(JsonNode? result)
    {
        if (result is not JsonObject document)
        {
            throw new InvalidOperationException("Result document is missing or invalid.");
        }

        var normalized = new JsonObject
        {
            ["packageId"] = document["packageId"]?.GetValue<string>(),
            ["version"] = document["version"]?.GetValue<string>(),
            ["disposition"] = document["disposition"]?.GetValue<string>(),
            ["retryEligible"] = document["retryEligible"]?.GetValue<bool>(),
            ["phase"] = document["phase"]?.GetValue<string>(),
            ["classification"] = document["classification"]?.GetValue<string>(),
            ["failureMessage"] = document["failureMessage"]?.GetValue<string>(),
            ["failureSignature"] = document["failureSignature"]?.GetValue<string>(),
            ["command"] = document["command"]?.GetValue<string>(),
            ["cliFramework"] = document["cliFramework"]?.GetValue<string>(),
            ["analysisMode"] = document["analysisMode"]?.GetValue<string>(),
        };

        AddStringIfPresent(normalized, "opencliSource", document["opencliSource"]?.GetValue<string>());
        AddObjectIfNotEmpty(normalized, "analysisSelection", NormalizeAnalysisSelection(document["analysisSelection"] as JsonObject));
        AddObjectIfNotEmpty(normalized, "fallback", NormalizeFallback(document["fallback"] as JsonObject));

        return normalized;
    }

    private static JsonObject NormalizeAnalysisSelection(JsonObject? analysisSelection)
    {
        var normalized = new JsonObject();
        if (analysisSelection is null)
        {
            return normalized;
        }

        AddStringIfPresent(normalized, "preferredMode", analysisSelection["preferredMode"]?.GetValue<string>());
        AddStringIfPresent(normalized, "selectedMode", analysisSelection["selectedMode"]?.GetValue<string>());
        AddStringIfPresent(normalized, "reason", analysisSelection["reason"]?.GetValue<string>());
        return normalized;
    }

    private static JsonObject NormalizeFallback(JsonObject? fallback)
    {
        var normalized = new JsonObject();
        if (fallback is null)
        {
            return normalized;
        }

        AddStringIfPresent(normalized, "from", fallback["from"]?.GetValue<string>());
        AddStringIfPresent(normalized, "disposition", fallback["disposition"]?.GetValue<string>());
        AddStringIfPresent(normalized, "classification", fallback["classification"]?.GetValue<string>());
        AddStringIfPresent(normalized, "message", fallback["message"]?.GetValue<string>());
        return normalized;
    }

    private static void AddStringIfPresent(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[propertyName] = value;
        }
    }

    private static void AddObjectIfNotEmpty(JsonObject target, string propertyName, JsonObject value)
    {
        if (value.Count > 0)
        {
            target[propertyName] = value;
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
            "HookResultSnapshots",
            $"{NormalizeSegment(packageId)}--{NormalizeSegment(version)}.json");
    }

    private static string NormalizeSegment(string value)
        => string.Concat(value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-'));

    private static string Serialize(JsonNode value)
        => JsonSerializer.Serialize(value, JsonOptions);
}
