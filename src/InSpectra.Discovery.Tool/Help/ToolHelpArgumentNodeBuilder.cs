namespace InSpectra.Discovery.Tool.Help;

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal sealed partial class ToolHelpArgumentNodeBuilder
{
    private static readonly HashSet<string> ArgumentNoiseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "A", "AN", "AND", "DEFAULT", "ENTER", "FOR", "OF", "OPTIONAL", "OR", "PRESS", "THE", "TO", "USE",
    };

    private static readonly HashSet<string> GenericArgumentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ARG",
        "ARGS",
        "VALUE",
    };

    public JsonArray? Build(string commandName, string commandPath, ToolHelpDocument? helpDocument)
    {
        if (helpDocument is null)
        {
            return null;
        }

        var explicitArguments = helpDocument.Arguments;
        if (ToolHelpSignatureNormalizer.IsBuiltinAuxiliaryCommand(commandPath)
            && (ToolHelpUsageArgumentSupport.LooksLikeCommandInventoryEchoArguments(explicitArguments, helpDocument.Commands)
                || ToolHelpUsageArgumentSupport.LooksLikeAuxiliaryInventoryEchoArguments(explicitArguments, helpDocument.UsageLines)))
        {
            explicitArguments = [];
        }

        var usageArguments = ToolHelpUsageArgumentSupport.ExtractUsageArguments(
            commandName,
            commandPath,
            helpDocument.UsageLines,
            helpDocument.Commands.Count > 0);
        var arguments = ToolHelpUsageArgumentSupport.SelectArguments(explicitArguments, usageArguments);
        if (arguments.Count == 0)
        {
            if (ToolHelpSignatureNormalizer.IsBuiltinAuxiliaryCommand(commandPath))
            {
                return null;
            }

            arguments = ToolHelpOptionDescriptionArgumentInference.Infer(helpDocument.Options);
        }

        if (arguments.Count == 0)
        {
            return null;
        }

        var array = new JsonArray();
        foreach (var argument in arguments)
        {
            if (!TryParseArgumentSignature(argument.Key, out var signature))
            {
                continue;
            }

            var node = new JsonObject
            {
                ["name"] = signature.Name,
                ["required"] = argument.IsRequired,
                ["hidden"] = false,
                ["arity"] = BuildArity(argument.IsRequired ? 1 : 0, signature.IsSequence),
            };

            if (!string.IsNullOrWhiteSpace(argument.Description))
            {
                node["description"] = argument.Description;
            }

            array.Add(node);
        }

        return array.Count > 0 ? array : null;
    }

    public static bool TryParseArgumentSignature(string rawKey, out ToolHelpArgumentSignature signature)
    {
        signature = new ToolHelpArgumentSignature(string.Empty, false);
        var trimmed = rawKey.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || ToolHelpOptionSignatureSupport.LooksLikeOptionPlaceholder(trimmed))
        {
            return false;
        }

        var isSequence = trimmed.Contains("...", StringComparison.Ordinal);
        var rawTokens = trimmed
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeArgumentToken)
            .Where(token => token.Length > 0)
            .ToArray();
        if (rawTokens.Length == 0 || ArgumentNoiseWords.Contains(rawTokens[0]))
        {
            return false;
        }

        string normalizedName;
        if (TryGetCommonPlaceholderStem(rawTokens, out var commonStem))
        {
            normalizedName = commonStem;
            isSequence = true;
        }
        else if (rawTokens.Length is > 1 and <= 3 && rawTokens.All(token => !ArgumentNoiseWords.Contains(token)))
        {
            normalizedName = string.Join('_', rawTokens);
        }
        else
        {
            normalizedName = rawTokens[0];
        }

        normalizedName = ToolHelpOptionSignatureSupport.NormalizeArgumentName(normalizedName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        signature = new ToolHelpArgumentSignature(normalizedName, isSequence);
        return true;
    }

    public static bool IsLowSignalExplicitArgument(ToolHelpItem argument)
        => TryParseArgumentSignature(argument.Key, out var signature)
            && GenericArgumentNames.Contains(signature.Name)
            && string.IsNullOrWhiteSpace(argument.Description);

    private static JsonObject BuildArity(int minimum, bool isSequence = false)
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

    private static bool TryGetCommonPlaceholderStem(IReadOnlyList<string> tokens, out string stem)
    {
        stem = string.Empty;
        if (tokens.Count < 2)
        {
            return false;
        }

        var stems = tokens
            .Where(token => !string.Equals(token, "...", StringComparison.Ordinal))
            .Select(token => TrailingDigitsRegex().Replace(token, string.Empty))
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (stems.Length != 1)
        {
            return false;
        }

        stem = stems[0];
        return true;
    }

    private static string NormalizeArgumentToken(string token)
    {
        var normalized = token.Trim()
            .Trim('[', ']', '<', '>', '(', ')', '{', '}', '.', ',', ':', ';', '"', '\'');
        normalized = normalized.Replace("...", string.Empty, StringComparison.Ordinal);
        normalized = InvalidArgumentTokenRegex().Replace(normalized, string.Empty);
        return normalized;
    }

    [GeneratedRegex(@"[^A-Za-z0-9_\-]", RegexOptions.Compiled)]
    private static partial Regex InvalidArgumentTokenRegex();

    [GeneratedRegex(@"\d+$", RegexOptions.Compiled)]
    private static partial Regex TrailingDigitsRegex();
}

internal sealed record ToolHelpArgumentSignature(
    string Name,
    bool IsSequence);

