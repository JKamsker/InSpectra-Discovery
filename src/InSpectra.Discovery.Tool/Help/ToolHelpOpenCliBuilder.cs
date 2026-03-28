using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal sealed partial class ToolHelpOpenCliBuilder
{
    private static readonly HashSet<string> ArgumentNoiseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "A",
        "AN",
        "AND",
        "DEFAULT",
        "ENTER",
        "FOR",
        "KEY",
        "OF",
        "OPTIONAL",
        "OR",
        "PRESS",
        "THE",
        "TO",
        "USE",
    };

    private readonly ToolHelpCommandTreeBuilder _commandTreeBuilder = new();

    public JsonObject Build(
        string commandName,
        string packageVersion,
        IReadOnlyDictionary<string, ToolHelpDocument> helpDocuments)
    {
        helpDocuments.TryGetValue(string.Empty, out var rootHelp);
        var rootCommands = new JsonArray(_commandTreeBuilder
            .Build(commandName, helpDocuments)
            .Select(node => BuildCommandNode(commandName, node, helpDocuments))
            .ToArray());
        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = BuildInfo(commandName, packageVersion, rootHelp),
            ["x-inspectra"] = new JsonObject
            {
                ["artifactSource"] = "crawled-from-help",
                ["generator"] = "InSpectra.Discovery",
                ["helpDocumentCount"] = helpDocuments.Count,
            },
            ["commands"] = rootCommands,
        };

        AddIfPresent(document, "options", BuildOptions(rootHelp));
        AddIfPresent(document, "arguments", BuildArguments(commandName, string.Empty, rootHelp));
        return OpenCliDocumentSanitizer.Sanitize(document);
    }

    private JsonObject BuildCommandNode(
        string commandName,
        ToolHelpCommandNode commandNode,
        IReadOnlyDictionary<string, ToolHelpDocument> helpDocuments)
    {
        helpDocuments.TryGetValue(commandNode.FullName, out var helpDocument);
        var node = new JsonObject
        {
            ["name"] = commandNode.DisplayName,
            ["hidden"] = false,
        };

        AddIfPresent(node, "description", helpDocument?.CommandDescription ?? commandNode.Description);

        var options = BuildOptions(helpDocument);
        AddIfPresent(node, "options", options);

        var arguments = BuildArguments(commandName, commandNode.FullName, helpDocument);
        AddIfPresent(node, "arguments", arguments);

        if (commandNode.Children.Count > 0)
        {
            node["commands"] = new JsonArray(commandNode.Children
                .Select(child => BuildCommandNode(commandName, child, helpDocuments))
                .ToArray());
        }

        return node;
    }

    private JsonArray? BuildOptions(ToolHelpDocument? helpDocument)
    {
        if (helpDocument?.Options.Count is not > 0)
        {
            return null;
        }

        var options = new JsonArray();
        foreach (var item in helpDocument.Options)
        {
            var signature = ParseOptionSignature(item.Key);
            if (signature.PrimaryName is null)
            {
                continue;
            }

            var node = new JsonObject
            {
                ["name"] = signature.PrimaryName,
                ["recursive"] = false,
                ["hidden"] = false,
            };

            AddIfPresent(node, "description", item.Description);

            if (signature.Aliases.Count > 0)
            {
                node["aliases"] = new JsonArray(signature.Aliases.Select(alias => JsonValue.Create(alias)).ToArray());
            }

            if (signature.ArgumentName is not null)
            {
                node["arguments"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = signature.ArgumentName.ToUpperInvariant(),
                        ["required"] = signature.ArgumentRequired,
                        ["arity"] = BuildArity(signature.ArgumentRequired ? 1 : 0),
                    },
                };
            }

            options.Add(node);
        }

        return options.Count > 0 ? options : null;
    }

    private JsonArray? BuildArguments(string commandName, string commandPath, ToolHelpDocument? helpDocument)
    {
        if (helpDocument is null)
        {
            return null;
        }

        var arguments = helpDocument.Arguments.Count > 0
            ? helpDocument.Arguments
            : ExtractUsageArguments(commandName, commandPath, helpDocument.UsageLines, helpDocument.Commands.Count > 0);

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

            AddIfPresent(node, "description", argument.Description);
            array.Add(node);
        }

        return array.Count > 0 ? array : null;
    }

    private static JsonObject BuildInfo(string commandName, string packageVersion, ToolHelpDocument? rootHelp)
    {
        var info = new JsonObject
        {
            ["title"] = rootHelp?.Title ?? commandName,
            ["version"] = rootHelp?.Version ?? packageVersion,
        };

        AddIfPresent(info, "description", rootHelp?.CommandDescription ?? rootHelp?.ApplicationDescription);
        return info;
    }

    private static void AddIfPresent(JsonObject target, string propertyName, JsonNode? value)
    {
        if (value is not null)
        {
            target[propertyName] = value;
        }
    }

    private static void AddIfPresent(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[propertyName] = value;
        }
    }

    private static IReadOnlyList<ToolHelpItem> ExtractUsageArguments(
        string commandName,
        string commandPath,
        IReadOnlyList<string> usageLines,
        bool hasChildCommands)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arguments = new List<ToolHelpItem>();

        foreach (var line in usageLines)
        {
            foreach (Match match in UsageArgumentRegex().Matches(line))
            {
                var value = match.Groups["name"].Value.Trim();
                if (LooksLikeOptionPlaceholder(value))
                {
                    continue;
                }

                if (AppearsInOptionClause(line, match))
                {
                    continue;
                }

                if (IsDispatcherPlaceholder(value))
                {
                    if (hasChildCommands)
                    {
                        break;
                    }

                    continue;
                }

                if (string.Equals(value, "options", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!seen.Add(value))
                {
                    continue;
                }

                arguments.Add(new ToolHelpItem(
                    Key: value,
                    IsRequired: !match.Value.StartsWith("[", StringComparison.Ordinal),
                    Description: null));
            }
        }

        return arguments;
    }

    private static bool IsDispatcherPlaceholder(string value)
        => string.Equals(value, "command", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "subcommand", StringComparison.OrdinalIgnoreCase);

    private static OptionSignature ParseOptionSignature(string key)
    {
        var aliases = new List<string>();
        var placeholders = UsageArgumentRegex().Matches(key)
            .Select(match => match.Groups["name"].Value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();
        var barePlaceholder = placeholders.Length == 0
            ? ExtractBareOptionPlaceholder(key)
            : null;

        foreach (var segment in key.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = OptionTokenRegex().Match(segment).Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            aliases.Add(token);
        }

        var primary = aliases
            .OrderByDescending(name => name.StartsWith("--", StringComparison.Ordinal) || name.StartsWith("/", StringComparison.Ordinal))
            .ThenByDescending(name => name.Length)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return new OptionSignature(
            PrimaryName: primary,
            Aliases: aliases
                .Where(alias => !string.Equals(alias, primary, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ArgumentName: NormalizeOptionArgumentName(placeholders.FirstOrDefault() ?? barePlaceholder, primary),
            ArgumentRequired: !key.Contains("[", StringComparison.Ordinal));
    }

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

    private static bool TryParseArgumentSignature(string rawKey, out ArgumentSignature signature)
    {
        signature = new ArgumentSignature(string.Empty, false);
        var trimmed = rawKey.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (LooksLikeOptionPlaceholder(trimmed))
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
        else
        {
            normalizedName = rawTokens[0];
        }

        normalizedName = NormalizeArgumentName(normalizedName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        signature = new ArgumentSignature(normalizedName, isSequence);
        return true;
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

    private static string? NormalizeOptionArgumentName(string? rawPlaceholder, string? primaryOption)
    {
        if (string.IsNullOrWhiteSpace(rawPlaceholder))
        {
            return null;
        }

        if (rawPlaceholder.Contains('|', StringComparison.Ordinal) || LooksLikeOptionPlaceholder(rawPlaceholder))
        {
            return InferArgumentNameFromOption(primaryOption);
        }

        return TryParseArgumentSignature(rawPlaceholder, out var signature)
            ? signature.Name
            : InferArgumentNameFromOption(primaryOption);
    }

    private static string? InferArgumentNameFromOption(string? primaryOption)
    {
        if (string.IsNullOrWhiteSpace(primaryOption))
        {
            return null;
        }

        var token = primaryOption.TrimStart('-', '/');
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var separator = token.IndexOfAny(['=', ':']);
        if (separator >= 0)
        {
            token = token[..separator];
        }

        return NormalizeArgumentName(token);
    }

    private static string? ExtractBareOptionPlaceholder(string key)
    {
        var matches = OptionTokenRegex().Matches(key);
        if (matches.Count == 0)
        {
            return null;
        }

        var trailing = key[(matches[^1].Index + matches[^1].Length)..].Trim();
        return IsBareOptionPlaceholder(trailing) ? trailing : null;
    }

    private static bool IsBareOptionPlaceholder(string trailing)
        => !string.IsNullOrWhiteSpace(trailing)
            && !trailing.Contains(' ', StringComparison.Ordinal)
            && !trailing.StartsWith("<", StringComparison.Ordinal)
            && !trailing.StartsWith("[", StringComparison.Ordinal)
            && !trailing.StartsWith("-", StringComparison.Ordinal)
            && !trailing.StartsWith("/", StringComparison.Ordinal);

    private static bool LooksLikeOptionPlaceholder(string value)
        => value.StartsWith("-", StringComparison.Ordinal)
            || value.StartsWith("/", StringComparison.Ordinal)
            || (value.Contains('|', StringComparison.Ordinal) && OptionTokenRegex().Match(value).Success)
            || (value.Contains('=', StringComparison.Ordinal) && OptionTokenRegex().Match(value).Success);

    private static bool AppearsInOptionClause(string line, Match match)
    {
        var index = match.Index - 1;
        while (index >= 0 && char.IsWhiteSpace(line[index]))
        {
            index--;
        }

        if (index < 0)
        {
            return false;
        }

        var tokenEnd = index;
        while (index >= 0 && !char.IsWhiteSpace(line[index]) && line[index] is not '[' and not '(' and not '{')
        {
            index--;
        }

        var candidate = line[(index + 1)..(tokenEnd + 1)].TrimEnd('=', ':');
        return candidate.Length > 0 && OptionTokenRegex().Match(candidate).Success;
    }

    private static string NormalizeArgumentName(string key)
        => key.Replace('-', '_').ToUpperInvariant();

    [GeneratedRegex(@"(?<option>(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*))", RegexOptions.Compiled)]
    private static partial Regex OptionTokenRegex();

    [GeneratedRegex(@"(?<all>\[?<(?<name>[^>]+)>\]?)", RegexOptions.Compiled)]
    private static partial Regex UsageArgumentRegex();

    [GeneratedRegex(@"[^A-Za-z0-9_\-]", RegexOptions.Compiled)]
    private static partial Regex InvalidArgumentTokenRegex();

    [GeneratedRegex(@"\d+$", RegexOptions.Compiled)]
    private static partial Regex TrailingDigitsRegex();

    private sealed record OptionSignature(
        string? PrimaryName,
        IReadOnlyList<string> Aliases,
        string? ArgumentName,
        bool ArgumentRequired);

    private sealed record ArgumentSignature(
        string Name,
        bool IsSequence);
}
