namespace InSpectra.Discovery.Tool.StaticAnalysis.OpenCli;

using InSpectra.Discovery.Tool.StaticAnalysis.Models;

using System.Text.Json.Nodes;

internal static class StaticAnalysisOpenCliNodeSupport
{
    public static JsonObject BuildArity(bool isSequence, int minimum)
    {
        var arity = new JsonObject { ["minimum"] = minimum };
        if (!isSequence)
        {
            arity["maximum"] = 1;
        }

        return arity;
    }

    public static (string? LongName, char? ShortName) ParseHelpOptionNames(string key)
    {
        string? longName = null;
        char? shortName = null;

        var parts = key.Split(',', '|', ' ');
        foreach (var raw in parts)
        {
            var part = TrimOptionValueSuffix(raw.Trim());
            if (part.StartsWith("--", StringComparison.Ordinal) && part.Length > 2)
            {
                longName = part[2..];
            }
            else if (part.StartsWith("-", StringComparison.Ordinal) && part.Length == 2 && char.IsLetterOrDigit(part[1]))
            {
                shortName = part[1];
            }
            else if (part.StartsWith("/", StringComparison.Ordinal) && part.Length > 1)
            {
                var slashToken = part[1..];
                if (slashToken.Length == 1 && char.IsLetterOrDigit(slashToken[0]))
                {
                    shortName = slashToken[0];
                }
                else if (slashToken.All(char.IsLetterOrDigit))
                {
                    longName = slashToken;
                }
            }
        }

        return (longName, shortName);
    }

    private static string TrimOptionValueSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var separatorIndex = value.IndexOfAny([':', '=', '<']);
        return separatorIndex >= 0
            ? value[..separatorIndex].TrimEnd()
            : value;
    }

    public static string NormalizeForLookup(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    public static string NormalizeArgumentName(string value)
    {
        var cleaned = value.Trim('-').Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "VALUE";
        }

        return string.Join("_", cleaned.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }

    public static void ApplyInputMetadata(JsonObject node, string? clrType, IReadOnlyList<string>? acceptedValues)
    {
        var metadata = new JsonArray();
        if (!string.IsNullOrWhiteSpace(clrType))
        {
            metadata.Add(new JsonObject
            {
                ["name"] = "ClrType",
                ["value"] = clrType,
            });
        }

        if (metadata.Count > 0)
        {
            node["metadata"] = metadata;
        }

        if (acceptedValues is { Count: > 0 })
        {
            node["acceptedValues"] = new JsonArray(acceptedValues.Select(v => JsonValue.Create(v)).ToArray());
        }
    }

    public static void AddIfPresent(JsonObject target, string propertyName, JsonNode? value)
    {
        if (value is not null)
        {
            target[propertyName] = value;
        }
    }

    public static void AddIfPresent(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[propertyName] = value;
        }
    }

    public static bool IsHiddenOption(StaticOptionDefinition definition)
    {
        var longName = definition.LongName;
        return longName is not null
            && (string.Equals(longName, "help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(longName, "version", StringComparison.OrdinalIgnoreCase));
    }
}
