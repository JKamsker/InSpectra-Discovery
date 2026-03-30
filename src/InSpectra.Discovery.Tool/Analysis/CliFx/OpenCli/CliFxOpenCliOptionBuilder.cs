namespace InSpectra.Discovery.Tool.Analysis.CliFx.OpenCli;

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal sealed class CliFxOpenCliOptionBuilder
{
    public JsonArray? BuildOptions(CliFxCommandDefinition? command, CliFxHelpDocument? helpDocument)
    {
        if (helpDocument?.Options.Count is not > 0 && command?.Options.Count is not > 0)
        {
            return null;
        }

        var array = new JsonArray();
        var matcher = new CliFxOptionDefinitionMatcher(command?.Options ?? []);
        foreach (var option in helpDocument?.Options ?? [])
        {
            var names = CliFxOptionNameSupport.Parse(option.Key);
            array.Add(BuildOptionNode(
                names.LongName is not null ? $"--{names.LongName}" : $"-{names.ShortName}",
                matcher.TakeMatch(names.LongName, names.ShortName),
                helpDocument,
                option.Description,
                option.IsRequired,
                names.LongName,
                names.ShortName));
        }

        foreach (var option in matcher.GetRemainingDefinitions())
        {
            array.Add(BuildOptionNode(
                CliFxOptionNameSupport.GetPrimaryName(option),
                option,
                null,
                option.Description,
                option.IsRequired,
                option.Name,
                option.ShortName));
        }

        return array.Count > 0 ? array : null;
    }

    private JsonObject BuildOptionNode(
        string name,
        CliFxOptionDefinition? definition,
        CliFxHelpDocument? helpDocument,
        string? description,
        bool required,
        string? longName,
        char? shortName)
    {
        var optionNode = new JsonObject
        {
            ["name"] = name,
            ["recursive"] = false,
            ["hidden"] = false,
        };
        CliFxOpenCliNodeSupport.AddIfPresent(optionNode, "description", description ?? definition?.Description);

        var aliases = CliFxOptionNameSupport.BuildAliases(definition, longName, shortName);
        if (aliases is not null)
        {
            optionNode["aliases"] = aliases;
        }

        var argument = BuildOptionArgument(
            definition,
            helpDocument?.UsageLines,
            definition?.ValueName ?? definition?.Name ?? longName ?? shortName?.ToString() ?? "VALUE",
            required,
            longName,
            shortName);
        if (argument is not null)
        {
            optionNode["arguments"] = new JsonArray { argument };
        }

        return optionNode;
    }

    private JsonObject? BuildOptionArgument(
        CliFxOptionDefinition? definition,
        IReadOnlyList<string>? usageLines,
        string fallbackName,
        bool required,
        string? longName,
        char? shortName)
    {
        if (definition is null)
        {
            var inferredName = InferOptionArgumentName(usageLines, longName, shortName);
            if (string.IsNullOrWhiteSpace(inferredName))
            {
                if (!required)
                {
                    return null;
                }

                inferredName = fallbackName;
            }

            return new JsonObject
            {
                ["name"] = CliFxOpenCliNodeSupport.NormalizeOptionArgumentName(inferredName),
                ["required"] = required,
                ["arity"] = CliFxOpenCliNodeSupport.BuildArity(false, required ? 1 : 0),
            };
        }

        var isNullableBool = string.Equals(definition.ClrType, "System.Nullable<System.Boolean>", StringComparison.Ordinal);
        if (!definition.IsRequired && definition.IsBoolLike && !isNullableBool)
        {
            return null;
        }

        var argument = new JsonObject
        {
            ["name"] = CliFxOpenCliNodeSupport.NormalizeOptionArgumentName(fallbackName),
            ["required"] = definition.IsRequired,
            ["arity"] = CliFxOpenCliNodeSupport.BuildArity(definition.IsSequence, definition.IsRequired ? 1 : 0),
        };

        CliFxOpenCliNodeSupport.ApplyInputMetadata(
            argument,
            definition.ClrType,
            definition.AcceptedValues,
            definition.EnvironmentVariable);
        return argument;
    }

    private static string? InferOptionArgumentName(IReadOnlyList<string>? usageLines, string? longName, char? shortName)
    {
        if (usageLines is not { Count: > 0 })
        {
            return null;
        }

        var tokens = new List<string>();
        if (!string.IsNullOrWhiteSpace(longName))
        {
            tokens.Add($"--{CliFxOptionNameSupport.NormalizeLongName(longName)}");
        }

        if (shortName is not null)
        {
            tokens.Add($"-{shortName}");
        }

        foreach (var usageLine in usageLines)
        {
            foreach (var token in tokens)
            {
                var placeholder = TryExtractUsagePlaceholder(usageLine, token);
                if (!string.IsNullOrWhiteSpace(placeholder))
                {
                    return placeholder;
                }
            }
        }

        return null;
    }

    private static string? TryExtractUsagePlaceholder(string usageLine, string token)
    {
        if (string.IsNullOrWhiteSpace(usageLine) || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var pattern = $@"(?:^|[\s\[(]){Regex.Escape(token)}(?:\s+|=)(?<placeholder><[^>]+>|[A-Z][A-Z0-9_-]*)";
        var match = Regex.Match(usageLine, pattern, RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var placeholder = match.Groups["placeholder"].Value.Trim();
        if (placeholder.StartsWith("<", StringComparison.Ordinal) && placeholder.EndsWith(">", StringComparison.Ordinal))
        {
            placeholder = placeholder[1..^1];
        }

        return string.IsNullOrWhiteSpace(placeholder) ? null : placeholder;
    }
}

