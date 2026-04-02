namespace InSpectra.Discovery.Tool.OpenCli.Options;

using InSpectra.Discovery.Tool.OpenCli.Structure;

using System.Text.Json.Nodes;

internal static class OpenCliOptionNameSanitizer
{
    public static void NormalizeOptionTokens(JsonObject option)
    {
        var publishableTokens = GetPublishableTokens(option);
        if (publishableTokens.Count == 0)
        {
            option.Remove("aliases");
            return;
        }

        var currentName = OpenCliValidationSupport.GetString(option["name"])?.Trim();
        var resolvedName = OpenCliNameValidationSupport.IsPublishableOptionName(currentName)
            ? currentName
            : publishableTokens[0];
        option["name"] = resolvedName;

        var aliases = new JsonArray();
        foreach (var token in publishableTokens)
        {
            if (string.Equals(token, resolvedName, StringComparison.Ordinal))
            {
                continue;
            }

            aliases.Add(token);
        }

        if (aliases.Count == 0)
        {
            option.Remove("aliases");
            return;
        }

        option["aliases"] = aliases;
    }

    public static bool HasPublishableOptionTokens(JsonObject option)
        => GetPublishableTokens(option).Count > 0;

    private static List<string> GetPublishableTokens(JsonObject option)
    {
        var tokens = new List<string>();
        foreach (var token in OpenCliOptionTokenValidationSupport.EnumerateOptionTokens(option))
        {
            if (!OpenCliNameValidationSupport.IsPublishableOptionName(token))
            {
                continue;
            }

            if (tokens.Any(existing => string.Equals(existing, token, StringComparison.Ordinal)))
            {
                continue;
            }

            tokens.Add(token);
        }

        return tokens;
    }
}
