namespace InSpectra.Discovery.Tool.Analysis.CliFx.OpenCli;

using InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;

using System.Text.Json.Nodes;

internal static class CliFxOptionNameSupport
{
    public static string GetPrimaryName(CliFxOptionDefinition option)
        => NormalizeLongName(option.Name) is { } normalizedName
            ? $"--{normalizedName}"
            : $"-{option.ShortName}";

    public static string? NormalizeLongName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        trimmed = trimmed.TrimStart('-');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    public static JsonArray? BuildAliases(CliFxOptionDefinition? definition, string? longName, char? shortName)
    {
        longName = NormalizeLongName(longName);
        var aliases = new JsonArray();
        if (longName is not null && shortName is not null)
        {
            aliases.Add($"-{shortName}");
        }
        else if (longName is null && NormalizeLongName(definition?.Name) is { } metadataLongName)
        {
            aliases.Add($"--{metadataLongName}");
        }
        else if (shortName is null && definition?.ShortName is not null)
        {
            aliases.Add($"-{definition.ShortName}");
        }

        return aliases.Count > 0 ? aliases : null;
    }

    public static (string? LongName, char? ShortName) Parse(string key)
    {
        string? longName = null;
        char? shortName = null;

        foreach (var token in key.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.StartsWith("--", StringComparison.Ordinal) && token.Length > 2)
            {
                longName = token[2..];
            }
            else if (token.StartsWith("-", StringComparison.Ordinal) && token.Length == 2)
            {
                shortName = token[1];
            }
        }

        return (longName, shortName);
    }
}

