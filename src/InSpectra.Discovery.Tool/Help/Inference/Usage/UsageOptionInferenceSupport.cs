namespace InSpectra.Discovery.Tool.Help.Inference.Usage;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.OpenCli;
using InSpectra.Discovery.Tool.Help.Signatures;

using System.Text.RegularExpressions;

internal static partial class UsageOptionInferenceSupport
{
    public static IReadOnlyList<Item> ExtractOptions(
        string rootCommandName,
        string commandPath,
        IReadOnlyList<string> usageLines)
    {
        var prototypes = UsagePrototypeSupport.ExtractLeafCommandPrototypes(rootCommandName, commandPath, usageLines);
        if (prototypes.Count == 0)
        {
            return [];
        }

        var options = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        foreach (var prototype in prototypes)
        {
            var tokens = prototype.Prototype
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var index = 0; index < tokens.Length; index++)
            {
                var optionName = NormalizeOptionToken(tokens[index]);
                if (optionName is null)
                {
                    continue;
                }

                var optionKey = optionName;
                if (TryGetOptionValueName(tokens, index + 1, out var valueName))
                {
                    optionKey = $"{optionName} <{valueName}>";
                    index++;
                }

                if (!options.ContainsKey(optionKey))
                {
                    options[optionKey] = new Item(optionKey, false, null);
                }
            }
        }

        return options.Values.ToArray();
    }

    private static string? NormalizeOptionToken(string token)
    {
        var trimmed = token.Trim().TrimStart('[', '(').TrimEnd(']', ')', ',', ';');
        if (trimmed.Length == 0
            || (trimmed[0] is not '-' and not '/'))
        {
            return null;
        }

        var signature = OptionSignatureSupport.Parse(trimmed);
        return signature.PrimaryName;
    }

    private static bool TryGetOptionValueName(
        IReadOnlyList<string> tokens,
        int index,
        out string valueName)
    {
        valueName = string.Empty;
        if (index >= tokens.Count)
        {
            return false;
        }

        var rawToken = tokens[index].Trim();
        if (rawToken.Length == 0 || NormalizeOptionToken(rawToken) is not null)
        {
            return false;
        }

        var candidate = NormalizeValueCandidate(rawToken);
        if (candidate is null
            || UsageArgumentPatternSupport.IsDispatcherPlaceholder(candidate)
            || UsageArgumentPatternSupport.IsOptionsPlaceholder(candidate)
            || !ArgumentNodeBuilder.TryParseArgumentSignature(candidate, out var signature))
        {
            return false;
        }

        valueName = signature.Name;
        return true;
    }

    private static string? NormalizeValueCandidate(string token)
    {
        var trimmed = token.Trim().Trim(',', ';');
        if (trimmed.Length == 0)
        {
            return null;
        }

        var wrapped = trimmed.StartsWith("<", StringComparison.Ordinal)
            || trimmed.StartsWith("[", StringComparison.Ordinal)
            || trimmed.StartsWith("(", StringComparison.Ordinal)
            || trimmed.StartsWith("{", StringComparison.Ordinal);
        if (trimmed.StartsWith("@", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..];
            wrapped = true;
        }

        trimmed = trimmed.Trim('[', ']', '<', '>', '(', ')', '{', '}', '"', '\'');
        if (trimmed.EndsWith("...", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^3];
        }

        trimmed = InvalidValueTokenRegex().Replace(trimmed, string.Empty);
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (!wrapped && !LooksLikeMetavar(trimmed))
        {
            return null;
        }

        return trimmed;
    }

    private static bool LooksLikeMetavar(string token)
        => token.Any(char.IsLetter)
            && string.Equals(token, token.ToUpperInvariant(), StringComparison.Ordinal);

    [GeneratedRegex(@"[^A-Za-z0-9_\-]", RegexOptions.Compiled)]
    private static partial Regex InvalidValueTokenRegex();
}
