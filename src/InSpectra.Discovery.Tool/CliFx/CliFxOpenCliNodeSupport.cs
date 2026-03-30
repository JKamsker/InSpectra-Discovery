using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class CliFxOpenCliNodeSupport
{
    public static JsonObject BuildArity(bool isSequence, int minimum)
    {
        var arity = new JsonObject
        {
            ["minimum"] = minimum,
        };

        if (!isSequence)
        {
            arity["maximum"] = 1;
        }

        return arity;
    }

    public static void ApplyInputMetadata(
        JsonObject node,
        string? clrType,
        IReadOnlyList<string>? acceptedValues,
        string? environmentVariable)
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

        if (!string.IsNullOrWhiteSpace(environmentVariable))
        {
            metadata.Add(new JsonObject
            {
                ["name"] = "EnvironmentVariable",
                ["value"] = environmentVariable,
            });
        }

        if (metadata.Count > 0)
        {
            node["metadata"] = metadata;
        }

        if (acceptedValues is { Count: > 0 })
        {
            node["acceptedValues"] = new JsonArray(acceptedValues.Select(value => JsonValue.Create(value)).ToArray());
        }
    }

    public static string NormalizeOptionArgumentName(string fallbackName)
    {
        fallbackName = CliFxOptionNameSupport.NormalizeLongName(fallbackName) ?? fallbackName;
        if (string.IsNullOrWhiteSpace(fallbackName))
        {
            return "VALUE";
        }

        var builder = new StringBuilder();
        char? previous = null;
        foreach (var character in fallbackName)
        {
            if (!char.IsLetterOrDigit(character))
            {
                if (builder.Length > 0 && builder[^1] != '_')
                {
                    builder.Append('_');
                }

                previous = character;
                continue;
            }

            if (builder.Length > 0
                && char.IsUpper(character)
                && previous is { } previousValue
                && (char.IsLower(previousValue) || char.IsDigit(previousValue))
                && builder[^1] != '_')
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(character));
            previous = character;
        }

        var normalized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "VALUE" : normalized;
    }

    public static string NormalizeParameterLookupKey(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim(), @"[^A-Za-z0-9]+", string.Empty).ToLowerInvariant();

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
}
