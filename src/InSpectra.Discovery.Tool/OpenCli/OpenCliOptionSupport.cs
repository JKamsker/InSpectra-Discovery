namespace InSpectra.Discovery.Tool.OpenCli;

using System.Text.Json.Nodes;

internal static class OpenCliOptionSupport
{
    public static JsonObject MergeOptions(JsonObject preferred, JsonObject other)
    {
        var merged = (JsonObject)preferred.DeepClone();
        if (merged["aliases"] is not JsonArray aliases)
        {
            aliases = new JsonArray();
            merged["aliases"] = aliases;
        }

        var existingAliases = aliases
            .OfType<JsonValue>()
            .Select(value => value.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        var primaryName = merged["name"]?.GetValue<string>();
        foreach (var token in GetOptionTokens(other))
        {
            if (string.IsNullOrWhiteSpace(token)
                || string.Equals(token, primaryName, StringComparison.Ordinal)
                || existingAliases.Contains(token))
            {
                continue;
            }

            aliases.Add(token);
            existingAliases.Add(token);
        }

        if (merged["arguments"] is null && other["arguments"] is JsonArray arguments)
        {
            merged["arguments"] = arguments.DeepClone();
        }

        if (string.IsNullOrWhiteSpace(merged["description"]?.GetValue<string>())
            && !string.IsNullOrWhiteSpace(other["description"]?.GetValue<string>()))
        {
            merged["description"] = other["description"]!.DeepClone();
        }

        if (!string.IsNullOrWhiteSpace(merged["description"]?.GetValue<string>()))
        {
            merged["description"] = OpenCliOptionDescriptionSupport.TrimTrailingDescriptionNoise(
                merged["description"]!.GetValue<string>());
        }

        if (aliases.Count == 0)
        {
            merged.Remove("aliases");
        }

        return merged;
    }

    public static HashSet<string> GetOptionTokens(JsonObject option)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var name = option["name"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(name))
        {
            tokens.Add(name);
        }

        if (option["aliases"] is not JsonArray aliases)
        {
            return tokens;
        }

        foreach (var alias in aliases.OfType<JsonValue>())
        {
            var token = alias.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(token))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    public static bool IsStandaloneAliasOption(JsonObject option)
    {
        var name = option["name"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(name)
            && name.StartsWith("-", StringComparison.Ordinal)
            && !name.StartsWith("--", StringComparison.Ordinal)
            && option["aliases"] is not JsonArray;
    }

    public static bool HasArguments(JsonObject option)
        => option["arguments"] is JsonArray arguments && arguments.Count > 0;

    public static string DeriveSyntheticArgumentName(string? optionName)
        => string.IsNullOrWhiteSpace(optionName)
            ? string.Empty
            : string.Concat(
                    optionName
                        .Trim()
                        .TrimStart('-', '/')
                        .Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_'))
                .Trim('_');
}

