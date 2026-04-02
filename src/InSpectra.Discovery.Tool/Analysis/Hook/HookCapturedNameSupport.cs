namespace InSpectra.Discovery.Tool.Analysis.Hook;

using InSpectra.Discovery.Tool.Help.Signatures;
using InSpectra.Discovery.Tool.OpenCli.Structure;

internal static class HookCapturedNameSupport
{
    public static string? ResolveOptionName(HookCapturedOption option)
    {
        var publishableTokens = GetPublishableOptionTokens(option);
        if (publishableTokens.Count == 0)
        {
            return null;
        }

        var rawName = option.Name?.Trim();
        return OpenCliNameValidationSupport.IsPublishableOptionName(rawName)
            ? rawName
            : publishableTokens[0];
    }

    public static IReadOnlyList<string> ResolveOptionAliases(HookCapturedOption option, string primaryName)
        => GetPublishableOptionTokens(option)
            .Where(token => !string.Equals(token, primaryName, StringComparison.Ordinal))
            .ToArray();

    public static string? ResolveOptionArgumentName(HookCapturedOption option)
    {
        var rawName = option.ArgumentName?.Trim();
        if (OpenCliNameValidationSupport.IsPublishableArgumentName(rawName))
        {
            return OptionSignatureSupport.NormalizeArgumentName(rawName!);
        }

        var inferredName = OptionSignatureSupport.InferArgumentNameFromOption(option.Name);
        if (OpenCliNameValidationSupport.IsPublishableArgumentName(inferredName))
        {
            return inferredName;
        }

        return null;
    }

    public static string ResolvePositionalArgumentName(HookCapturedArgument argument, int index)
    {
        var rawName = argument.Name?.Trim();
        if (OpenCliNameValidationSupport.IsPublishableArgumentName(rawName))
        {
            return OptionSignatureSupport.NormalizeArgumentName(rawName!);
        }

        return index == 0 ? "VALUE" : $"VALUE_{index + 1}";
    }

    private static List<string> GetPublishableOptionTokens(HookCapturedOption option)
    {
        var tokens = new List<string>();
        AddPublishableOptionToken(option.Name, tokens);
        foreach (var alias in option.Aliases)
        {
            AddPublishableOptionToken(alias, tokens);
        }

        return tokens;
    }

    private static void AddPublishableOptionToken(string? rawToken, ICollection<string> tokens)
    {
        var trimmed = rawToken?.Trim();
        if (!OpenCliNameValidationSupport.IsPublishableOptionName(trimmed))
        {
            return;
        }

        if (tokens.Any(existing => string.Equals(existing, trimmed, StringComparison.Ordinal)))
        {
            return;
        }

        tokens.Add(trimmed!);
    }
}
