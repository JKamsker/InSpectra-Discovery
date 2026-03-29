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
        "OF",
        "OPTIONAL",
        "OR",
        "PRESS",
        "THE",
        "TO",
        "USE",
    };
    private static readonly HashSet<string> GenericArgumentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ARG",
        "ARGS",
        "VALUE",
    };
    private static readonly HashSet<string> InformationalOptionDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Display this help screen.",
        "Display version information.",
        "Show help information.",
        "Show help and usage information",
    };
    private static readonly HashSet<string> ValueLikeOptionNameTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "access",
        "address",
        "api",
        "alias",
        "assembly",
        "baseline",
        "byte",
        "bytes",
        "branch",
        "certificate",
        "cert",
        "channel",
        "code",
        "codes",
        "column",
        "columns",
        "component",
        "config",
        "configuration",
        "conn",
        "connection",
        "comment",
        "comments",
        "count",
        "culture",
        "database",
        "directories",
        "dir",
        "directory",
        "dll",
        "duration",
        "email",
        "env",
        "environment",
        "etw",
        "expiry",
        "exclude",
        "file",
        "files",
        "filter",
        "folder",
        "format",
        "factory",
        "feature",
        "guid",
        "host",
        "id",
        "ids",
        "index",
        "indexes",
        "include",
        "input",
        "justification",
        "key",
        "kind",
        "language",
        "level",
        "license",
        "log",
        "migration",
        "model",
        "modifier",
        "method",
        "namespace",
        "name",
        "notes",
        "output",
        "package",
        "param",
        "parser",
        "password",
        "path",
        "pattern",
        "plugin",
        "policy",
        "port",
        "post",
        "producer",
        "producers",
        "provider",
        "project",
        "property",
        "prefix",
        "regex",
        "region",
        "regions",
        "repo",
        "repository",
        "result",
        "runtime",
        "rule",
        "save",
        "schema",
        "schemas",
        "search",
        "service",
        "server",
        "solution",
        "source",
        "status",
        "subscription",
        "table",
        "tables",
        "target",
        "targets",
        "template",
        "threshold",
        "thread",
        "threads",
        "thumbprint",
        "timeout",
        "token",
        "topic",
        "tool",
        "trace",
        "type",
        "uri",
        "url",
        "value",
        "version",
        "xml",
        "xsl",
        "yaml",
        "yml",
        "zip",
        "size",
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

            var inferredArgumentRequired = StartsWithRequiredPrefix(item.Description);
            var hasExplicitArgument = signature.ArgumentName is not null;
            var hasNonBooleanDefault = HasNonBooleanDefault(item.Description ?? string.Empty);
            var argumentName = signature.ArgumentName
                ?? InferOptionArgumentNameFromDescription(signature, item.Description);
            var argumentRequired = argumentName is not null
                && (hasExplicitArgument
                    ? !hasNonBooleanDefault && (signature.ArgumentRequired || inferredArgumentRequired)
                    : inferredArgumentRequired);
            var description = StartsWithRequiredPrefix(item.Description)
                ? TrimLeadingRequiredPrefix(item.Description)
                : item.Description;

            var node = new JsonObject
            {
                ["name"] = signature.PrimaryName,
                ["recursive"] = false,
                ["hidden"] = false,
            };

            AddIfPresent(node, "description", description);

            if (signature.Aliases.Count > 0)
            {
                node["aliases"] = new JsonArray(signature.Aliases.Select(alias => JsonValue.Create(alias)).ToArray());
            }

            if (argumentName is not null)
            {
                node["arguments"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = argumentName.ToUpperInvariant(),
                        ["required"] = argumentRequired,
                        ["arity"] = BuildArity(argumentRequired ? 1 : 0),
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

        var explicitArguments = helpDocument.Arguments;
        if (IsBuiltinAuxiliaryCommand(commandPath)
            && (LooksLikeCommandInventoryEchoArguments(explicitArguments, helpDocument.Commands)
                || LooksLikeAuxiliaryInventoryEchoArguments(explicitArguments, helpDocument.UsageLines)))
        {
            explicitArguments = [];
        }

        var usageArguments = ExtractUsageArguments(commandName, commandPath, helpDocument.UsageLines, helpDocument.Commands.Count > 0);
        var arguments = SelectArguments(explicitArguments, usageArguments);
        if (arguments.Count == 0)
        {
            if (IsBuiltinAuxiliaryCommand(commandPath))
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
            ["version"] = string.IsNullOrWhiteSpace(packageVersion) ? rootHelp?.Version : packageVersion,
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
        string? previousNonEmptyLine = null;

        foreach (var line in usageLines)
        {
            var lineArguments = new List<ToolHelpItem>();
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

                var quantifier = GetUsageArgumentQuantifier(line, match);
                var isSequence = value.Contains("...", StringComparison.Ordinal) || quantifier is '*' or '+';
                var isRequired = !match.Value.StartsWith("[", StringComparison.Ordinal) && quantifier is not '*' and not '?';
                var normalizedKey = NormalizeUsageArgumentKey(value, isSequence);
                if (!seen.Add(normalizedKey))
                {
                    continue;
                }

                var argument = new ToolHelpItem(
                    Key: normalizedKey,
                    IsRequired: isRequired,
                    Description: null);
                lineArguments.Add(argument);
                arguments.Add(argument);
            }

            if (lineArguments.Count > 0)
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (LooksLikeWrappedUsageValueContinuation(previousNonEmptyLine, line))
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (!LooksLikeExampleLabel(previousNonEmptyLine))
            {
                var bareArgument = ExtractBareUsageArgument(commandName, commandPath, line, hasChildCommands);
                if (bareArgument is not null && seen.Add(bareArgument.Key))
                {
                    arguments.Add(bareArgument);
                }
            }
            previousNonEmptyLine = line;
        }

        return arguments;
    }

    private static IReadOnlyList<ToolHelpItem> SelectArguments(
        IReadOnlyList<ToolHelpItem> explicitArguments,
        IReadOnlyList<ToolHelpItem> usageArguments)
    {
        if (explicitArguments.Count == 0)
        {
            return usageArguments;
        }

        if (usageArguments.Count == 0)
        {
            return explicitArguments.All(IsLowSignalExplicitArgument)
                ? []
                : explicitArguments;
        }

        if (explicitArguments.Count != usageArguments.Count)
        {
            return explicitArguments.All(IsLowSignalExplicitArgument)
                ? usageArguments
                : explicitArguments;
        }

        var merged = new List<ToolHelpItem>(explicitArguments.Count);
        var changed = false;
        for (var index = 0; index < explicitArguments.Count; index++)
        {
            var explicitArgument = explicitArguments[index];
            var usageArgument = usageArguments[index];
            var mergedArgument = MergeArguments(explicitArgument, usageArgument);
            merged.Add(mergedArgument);

            changed |= !string.Equals(explicitArgument.Key, mergedArgument.Key, StringComparison.Ordinal)
                || explicitArgument.IsRequired != mergedArgument.IsRequired
                || !string.Equals(explicitArgument.Description, mergedArgument.Description, StringComparison.Ordinal);
        }

        return changed ? merged : explicitArguments;
    }

    private static ToolHelpItem MergeArguments(ToolHelpItem explicitArgument, ToolHelpItem usageArgument)
    {
        var chosenKey = explicitArgument.Key;
        if (TryParseArgumentSignature(explicitArgument.Key, out var explicitSignature)
            && TryParseArgumentSignature(usageArgument.Key, out var usageSignature))
        {
            var useUsageName = IsGenericArgumentName(explicitSignature.Name)
                && !IsGenericArgumentName(usageSignature.Name)
                && string.IsNullOrWhiteSpace(explicitArgument.Description);
            if (useUsageName)
            {
                chosenKey = usageArgument.Key;
            }
            else if (!explicitSignature.IsSequence && usageSignature.IsSequence)
            {
                chosenKey = NormalizeUsageArgumentKey(explicitArgument.Key, isSequence: true);
            }
        }

        return new ToolHelpItem(
            chosenKey,
            explicitArgument.IsRequired || usageArgument.IsRequired,
            explicitArgument.Description ?? usageArgument.Description);
    }

    private static bool IsDispatcherPlaceholder(string value)
        => string.Equals(value, "command", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "subcommand", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeExampleLabel(string? line)
        => !string.IsNullOrWhiteSpace(line)
            && line.TrimEnd().EndsWith(":", StringComparison.Ordinal)
            && !line.TrimStart().StartsWith("-", StringComparison.Ordinal);

    private static bool LooksLikeCommandInventoryEchoArguments(
        IReadOnlyList<ToolHelpItem> arguments,
        IReadOnlyList<ToolHelpItem> commands)
    {
        if (arguments.Count < 2 || commands.Count == 0)
        {
            return false;
        }

        var commandDescriptions = commands
            .Select(command => (Key: NormalizeCommandInventoryKey(command.Key), Description: NormalizeInlineText(command.Description)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Description))
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Description, StringComparer.OrdinalIgnoreCase);
        if (commandDescriptions.Count == 0)
        {
            return false;
        }

        return arguments.All(argument =>
        {
            var normalizedKey = NormalizeCommandInventoryKey(argument.Key);
            return !argument.IsRequired
                && commandDescriptions.TryGetValue(normalizedKey, out var commandDescription)
                && string.Equals(NormalizeInlineText(argument.Description), commandDescription, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool LooksLikeAuxiliaryInventoryEchoArguments(
        IReadOnlyList<ToolHelpItem> arguments,
        IReadOnlyList<string> usageLines)
    {
        if (arguments.Count < 2 || usageLines.Count > 0)
        {
            return false;
        }

        return arguments.All(argument =>
            !argument.IsRequired
            && !string.IsNullOrWhiteSpace(argument.Description)
            && !string.IsNullOrWhiteSpace(NormalizeCommandInventoryKey(argument.Key)));
    }

    private static bool IsBuiltinAuxiliaryCommand(string commandPath)
    {
        if (string.IsNullOrWhiteSpace(commandPath))
        {
            return false;
        }

        var leafSegment = ToolHelpCommandPathSupport.SplitSegments(commandPath).LastOrDefault();
        return string.Equals(leafSegment, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(leafSegment, "version", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeInlineText(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(
                ' ',
                value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string NormalizeCommandInventoryKey(string key)
    {
        var normalizedKey = key.Trim();
        var rawSegments = normalizedKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var segments = new List<string>();
        for (var index = 0; index < rawSegments.Length; index++)
        {
            var aliases = new List<string> { rawSegments[index].TrimEnd(',', ':') };
            while (rawSegments[index].EndsWith(",", StringComparison.Ordinal) && index + 1 < rawSegments.Length)
            {
                index++;
                aliases.Add(rawSegments[index].TrimEnd(',', ':'));
            }

            segments.Add(aliases
                .Where(alias => alias.Length > 0)
                .OrderByDescending(alias => alias.Length)
                .FirstOrDefault() ?? string.Empty);
        }

        var normalized = segments
            .TakeWhile(segment => !segment.StartsWith("<", StringComparison.Ordinal)
                && !segment.StartsWith("[", StringComparison.Ordinal)
                && !segment.StartsWith("(", StringComparison.Ordinal)
                && !segment.StartsWith("-", StringComparison.Ordinal)
                && !segment.StartsWith("/", StringComparison.Ordinal))
            .Where(segment => segment.Length > 0)
            .ToArray();
        return normalized.Length == 0 ? string.Empty : string.Join(' ', normalized);
    }

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

        var keyForAliasParsing = StripBracketedPlaceholders(key);
        foreach (var segment in keyForAliasParsing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string? previousToken = null;
            foreach (var pipeSegment in segment.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var token = TryParseOptionToken(pipeSegment, previousToken);
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                aliases.Add(token);
                previousToken = token;
            }
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
        else if (rawTokens.Length is > 1 and <= 3
            && rawTokens.All(token => !ArgumentNoiseWords.Contains(token)))
        {
            normalizedName = string.Join('_', rawTokens);
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

    private static string? InferOptionArgumentNameFromDescription(OptionSignature signature, string? description)
    {
        var primaryOption = signature.PrimaryName;
        if (string.IsNullOrWhiteSpace(primaryOption))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return HasValueLikeOptionName(primaryOption)
                ? InferArgumentNameFromOption(primaryOption)
                : null;
        }

        var normalizedDescription = NormalizeDescriptionForInference(description);
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return HasValueLikeOptionName(primaryOption)
                ? InferArgumentNameFromOption(primaryOption)
                : null;
        }

        var trimmedDescription = TrimLeadingRequiredPrefix(normalizedDescription) ?? normalizedDescription;
        var descriptionWithoutDefault = TrimLeadingDefaultClause(trimmedDescription);
        var hasNonBooleanDefault = HasNonBooleanDefault(trimmedDescription);
        var defaultValue = GetDefaultValue(trimmedDescription);
        var descriptionForSignals = NormalizeDescriptionForSignals(string.IsNullOrWhiteSpace(descriptionWithoutDefault)
            ? trimmedDescription
            : descriptionWithoutDefault);
        var hasRequiredPrefix = StartsWithRequiredPrefix(normalizedDescription);
        var hasInlineOptionExample = ContainsInlineOptionExample(signature, normalizedDescription);
        var hasExplicitValueEvidence = hasNonBooleanDefault
            || hasInlineOptionExample
            || ContainsIllustrativeValueExample(descriptionForSignals);
        var hasDescriptiveValueEvidence = ContainsStrongValueDescriptionHint(descriptionForSignals);
        if (string.IsNullOrWhiteSpace(trimmedDescription))
        {
            return hasRequiredPrefix && HasValueLikeOptionName(primaryOption)
                ? InferArgumentNameFromOption(primaryOption)
                : null;
        }

        if (IsBooleanDefaultValue(defaultValue) && !(hasExplicitValueEvidence || hasDescriptiveValueEvidence))
        {
            return null;
        }

        if (IsInformationalOptionDescription(trimmedDescription))
        {
            return null;
        }

        if (LooksLikeFlagDescription(descriptionForSignals))
        {
            var descriptiveOverride = hasDescriptiveValueEvidence
                && AllowsDescriptiveValueEvidenceToOverrideFlag(descriptionForSignals);
            var onlyDefaultBacksThis = hasNonBooleanDefault
                && !hasInlineOptionExample
                && !hasDescriptiveValueEvidence;
            if (onlyDefaultBacksThis || (!hasExplicitValueEvidence && !descriptiveOverride))
            {
                return null;
            }
        }

        if (hasRequiredPrefix
            || hasExplicitValueEvidence
            || hasDescriptiveValueEvidence
            || HasValueLikeOptionName(primaryOption))
        {
            return InferArgumentNameFromOption(primaryOption);
        }

        return null;
    }

    private static string NormalizeDescriptionForInference(string description)
        => string.Join(
            " ",
            description
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0))
            .Trim();

    private static string NormalizeDescriptionForSignals(string description)
    {
        var normalized = description.TrimStart();
        while (normalized.StartsWith("(", StringComparison.Ordinal))
        {
            var closingIndex = normalized.IndexOf(')');
            if (closingIndex < 0)
            {
                break;
            }

            normalized = normalized[(closingIndex + 1)..].TrimStart();
        }

        if (normalized.StartsWith("Override:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Override:".Length..].TrimStart();
        }

        return normalized;
    }

    private static bool StartsWithRequiredPrefix(string? description)
        => !string.IsNullOrWhiteSpace(description)
            && (
                description.TrimStart().StartsWith("Required.", StringComparison.OrdinalIgnoreCase)
                || description.TrimStart().StartsWith("Required ", StringComparison.OrdinalIgnoreCase)
                || description.TrimStart().StartsWith("(REQUIRED)", StringComparison.OrdinalIgnoreCase)
                || description.TrimStart().StartsWith("[REQUIRED]", StringComparison.OrdinalIgnoreCase));

    private static string? TrimLeadingRequiredPrefix(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        var normalized = description.TrimStart();
        if (normalized.StartsWith("Required.", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["Required.".Length..].TrimStart();
        }

        if (normalized.StartsWith("Required ", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["Required ".Length..].TrimStart();
        }

        if (normalized.StartsWith("(REQUIRED)", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["(REQUIRED)".Length..].TrimStart();
        }

        if (normalized.StartsWith("[REQUIRED]", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["[REQUIRED]".Length..].TrimStart();
        }

        return description;
    }

    private static bool IsInformationalOptionDescription(string description)
        => InformationalOptionDescriptions.Contains(description)
            || description.StartsWith("Display version information", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Display the program version", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Display this help", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Show version information", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Show help", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeFlagDescription(string description)
        => description.StartsWith("Actually ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Allow ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Append ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Check ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Continue ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Convert ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Create ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Creates ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Delete ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Determine ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Disable ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Display ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Don't ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Enable ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Enables ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Escape ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Exit ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Flatten ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Force ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Gather ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Generate ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Generates ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Hashes ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("If--", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("If --", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Include ", StringComparison.OrdinalIgnoreCase)
            || (description.StartsWith("List ", StringComparison.OrdinalIgnoreCase)
                && !description.StartsWith("List of ", StringComparison.OrdinalIgnoreCase))
            || description.StartsWith("Merge ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Merges ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Minify ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Overwrite ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Pack ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Packs ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Preserve ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Pretty ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Print ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Report ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Run ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Save ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Set true to ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Set false to ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Show ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Skip ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Skips ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Sort ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Suppress ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Toggle ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Update ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Use ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Verbose ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Wrap ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Whether ", StringComparison.OrdinalIgnoreCase);

    private static bool HasNonBooleanDefault(string description)
    {
        var defaultValue = GetDefaultValue(description);
        return !string.IsNullOrWhiteSpace(defaultValue)
            && !IsBooleanDefaultValue(defaultValue);
    }

    private static bool ContainsStrongValueDescriptionHint(string description)
        => description.Contains("build configuration", StringComparison.OrdinalIgnoreCase)
            || description.Contains("comma-separated", StringComparison.OrdinalIgnoreCase)
            || description.Contains("comma-separated list", StringComparison.OrdinalIgnoreCase)
            || description.Contains("supported values", StringComparison.OrdinalIgnoreCase)
            || description.Contains("file extension", StringComparison.OrdinalIgnoreCase)
            || description.Contains("file extensions", StringComparison.OrdinalIgnoreCase)
            || description.Contains("folder names", StringComparison.OrdinalIgnoreCase)
            || description.Contains("fully qualified names", StringComparison.OrdinalIgnoreCase)
            || description.Contains("input path", StringComparison.OrdinalIgnoreCase)
            || description.Contains("query parameter", StringComparison.OrdinalIgnoreCase)
            || description.Contains("package ids", StringComparison.OrdinalIgnoreCase)
            || description.Contains("output path", StringComparison.OrdinalIgnoreCase)
            || description.Contains("output file location", StringComparison.OrdinalIgnoreCase)
            || description.Contains("input file location", StringComparison.OrdinalIgnoreCase)
            || description.Contains("output file name", StringComparison.OrdinalIgnoreCase)
            || description.Contains("specified .net runtime", StringComparison.OrdinalIgnoreCase)
            || description.Contains("sort key and direction", StringComparison.OrdinalIgnoreCase)
            || description.Contains("text to put", StringComparison.OrdinalIgnoreCase)
            || description.Contains("url extension", StringComparison.OrdinalIgnoreCase)
            || description.Contains("aws region", StringComparison.OrdinalIgnoreCase)
            || description.Contains("attributes tolerance", StringComparison.OrdinalIgnoreCase)
            || description.Contains("friendly name", StringComparison.OrdinalIgnoreCase)
            || description.Contains("language tag", StringComparison.OrdinalIgnoreCase)
            || description.Contains("icon class", StringComparison.OrdinalIgnoreCase)
            || description.Contains("text color", StringComparison.OrdinalIgnoreCase)
            || description.Contains("background color", StringComparison.OrdinalIgnoreCase)
            || description.Contains("queue name", StringComparison.OrdinalIgnoreCase)
            || description.Contains("topic name", StringComparison.OrdinalIgnoreCase)
            || description.Contains("subscription name", StringComparison.OrdinalIgnoreCase)
            || description.Contains("free-form reference string", StringComparison.OrdinalIgnoreCase)
            || description.Contains("properties to categorize by", StringComparison.OrdinalIgnoreCase)
            || description.Contains("labels to add", StringComparison.OrdinalIgnoreCase)
            || description.Contains("header to write", StringComparison.OrdinalIgnoreCase)
            || description.Contains("game release", StringComparison.OrdinalIgnoreCase)
            || description.Contains("days to keep", StringComparison.OrdinalIgnoreCase)
            || description.Contains("must be positive", StringComparison.OrdinalIgnoreCase)
            || description.Contains("directory to", StringComparison.OrdinalIgnoreCase)
            || description.Contains("target folder", StringComparison.OrdinalIgnoreCase)
            || description.Contains("copyright", StringComparison.OrdinalIgnoreCase)
            || description.Contains("use to set the version", StringComparison.OrdinalIgnoreCase)
            || description.Contains("utc datetime", StringComparison.OrdinalIgnoreCase)
            || description.Contains("iso 8601", StringComparison.OrdinalIgnoreCase)
            || description.Contains("path to", StringComparison.OrdinalIgnoreCase)
            || description.Contains("path where", StringComparison.OrdinalIgnoreCase)
            || description.Contains("comma separated", StringComparison.OrdinalIgnoreCase)
            || description.Contains("connection string", StringComparison.OrdinalIgnoreCase)
            || description.Contains("custom defined name", StringComparison.OrdinalIgnoreCase)
            || description.Contains("definition string", StringComparison.OrdinalIgnoreCase)
            || description.Contains("directory that", StringComparison.OrdinalIgnoreCase)
            || description.Contains("file location", StringComparison.OrdinalIgnoreCase)
            || description.Contains("file name search pattern", StringComparison.OrdinalIgnoreCase)
            || description.Contains("glob patterns", StringComparison.OrdinalIgnoreCase)
            || description.Contains("for script in format", StringComparison.OrdinalIgnoreCase)
            || description.Contains("file path", StringComparison.OrdinalIgnoreCase)
            || description.Contains("file to", StringComparison.OrdinalIgnoreCase)
            || description.Contains("format '", StringComparison.OrdinalIgnoreCase)
            || description.Contains("format \"", StringComparison.OrdinalIgnoreCase)
            || description.Contains("input script", StringComparison.OrdinalIgnoreCase)
            || description.Contains("indent size", StringComparison.OrdinalIgnoreCase)
            || description.Contains("json format", StringComparison.OrdinalIgnoreCase)
            || description.Contains("max attributes", StringComparison.OrdinalIgnoreCase)
            || description.Contains("default value is", StringComparison.OrdinalIgnoreCase)
            || description.Contains("max attribute characters", StringComparison.OrdinalIgnoreCase)
            || description.Contains("namespace of", StringComparison.OrdinalIgnoreCase)
            || description.Contains("owner/repo", StringComparison.OrdinalIgnoreCase)
            || description.Contains("project files", StringComparison.OrdinalIgnoreCase)
            || description.Contains("expressed as", StringComparison.OrdinalIgnoreCase)
            || description.Contains("root directory", StringComparison.OrdinalIgnoreCase)
            || description.Contains("some|none|all", StringComparison.OrdinalIgnoreCase)
            || description.Contains("source folder", StringComparison.OrdinalIgnoreCase)
            || description.Contains("source repository", StringComparison.OrdinalIgnoreCase)
            || description.Contains("threshold", StringComparison.OrdinalIgnoreCase)
            || description.Contains("valid values", StringComparison.OrdinalIgnoreCase)
            || description.Contains("must be one of", StringComparison.OrdinalIgnoreCase)
            || description.Contains("enclosed in double quotes", StringComparison.OrdinalIgnoreCase)
            || description.Contains("semicolon-delimited", StringComparison.OrdinalIgnoreCase)
            || description.Contains("corresponding local file path", StringComparison.OrdinalIgnoreCase)
            || description.Contains("to which", StringComparison.OrdinalIgnoreCase)
            || description.Contains("persisted to", StringComparison.OrdinalIgnoreCase)
            || description.Contains("posted.", StringComparison.OrdinalIgnoreCase)
            || description.Contains("replace string", StringComparison.OrdinalIgnoreCase)
            || description.Contains("start folder", StringComparison.OrdinalIgnoreCase)
            || description.Contains("will be written", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("A ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("An ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Custom ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Duration", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Directory ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Extra ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Full pathname to ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Input ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Namespace ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Name of ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Number of ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Parameters for ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Prefix of ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Provide parameter", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Provide propert", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Set a ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Set an ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Set the ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("The name of ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Specify ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("The number of ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Type of ", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsIllustrativeValueExample(string description)
        => description.Contains("something like", StringComparison.OrdinalIgnoreCase)
            || description.Contains("specified .net runtime (", StringComparison.OrdinalIgnoreCase)
            || description.Contains("specified .net runtime ", StringComparison.OrdinalIgnoreCase);

    private static bool AllowsDescriptiveValueEvidenceToOverrideFlag(string description)
        => description.Contains("fully qualified names", StringComparison.OrdinalIgnoreCase)
            || description.Contains("separate by", StringComparison.OrdinalIgnoreCase)
            || description.Contains("separated by", StringComparison.OrdinalIgnoreCase)
            || description.Contains("use to set the version", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsInlineOptionExample(OptionSignature signature, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var normalized = NormalizeDescriptionForInference(description);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        foreach (var optionToken in EnumerateOptionTokens(signature))
        {
            var searchIndex = 0;
            while (searchIndex < normalized.Length)
            {
                var matchIndex = normalized.IndexOf(optionToken, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    break;
                }

                if (!HasInlineOptionExampleBoundary(normalized, matchIndex, optionToken.Length))
                {
                    searchIndex = matchIndex + optionToken.Length;
                    continue;
                }

                var valueStart = matchIndex + optionToken.Length;
                if (valueStart < normalized.Length
                    && char.IsWhiteSpace(normalized, valueStart))
                {
                    while (valueStart < normalized.Length && char.IsWhiteSpace(normalized, valueStart))
                    {
                        valueStart++;
                    }

                    if (valueStart < normalized.Length)
                    {
                        var next = normalized[valueStart];
                        if (!char.IsWhiteSpace(next)
                            && next is not '-' and not '/' and not '.' and not ',' and not ';' and not ')')
                        {
                            if (LooksLikeInlineReferenceWord(ReadInlineReferenceWord(normalized, valueStart)))
                            {
                                searchIndex = matchIndex + optionToken.Length;
                                continue;
                            }

                            return true;
                        }
                    }
                }

                searchIndex = matchIndex + optionToken.Length;
            }
        }

        return false;
    }

    private static bool HasInlineOptionExampleBoundary(string description, int matchIndex, int tokenLength)
    {
        if (matchIndex > 0)
        {
            var previous = description[matchIndex - 1];
            if (char.IsLetterOrDigit(previous))
            {
                return false;
            }
        }

        var endIndex = matchIndex + tokenLength;
        if (endIndex < description.Length)
        {
            var next = description[endIndex];
            if (char.IsLetterOrDigit(next))
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadInlineReferenceWord(string description, int startIndex)
    {
        var endIndex = startIndex;
        while (endIndex < description.Length)
        {
            var character = description[endIndex];
            if (char.IsWhiteSpace(character) || character is ',' or ';' or ')' or '(' or '[' or ']')
            {
                break;
            }

            endIndex++;
        }

        return description[startIndex..endIndex];
    }

    private static bool LooksLikeInlineReferenceWord(string word)
        => string.Equals(word, "is", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "was", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "are", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "used", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "specified", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "set", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "to", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "for", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "if", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "when", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateOptionTokens(OptionSignature signature)
    {
        if (!string.IsNullOrWhiteSpace(signature.PrimaryName))
        {
            yield return signature.PrimaryName;
        }

        foreach (var alias in signature.Aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)))
        {
            yield return alias;
        }
    }

    private static string? GetDefaultValue(string description)
    {
        var marker = "(Default:";
        var startIndex = description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return null;
        }

        var valueStart = startIndex + marker.Length;
        var endIndex = description.IndexOf(')', valueStart);
        if (endIndex <= valueStart)
        {
            return null;
        }

        return description[valueStart..endIndex].Trim();
    }

    private static string TrimLeadingDefaultClause(string description)
    {
        var normalized = description.TrimStart();
        if (!normalized.StartsWith("(Default:", StringComparison.OrdinalIgnoreCase))
        {
            return description;
        }

        var endIndex = normalized.IndexOf(')');
        return endIndex < 0
            ? string.Empty
            : normalized[(endIndex + 1)..].TrimStart();
    }

    private static bool IsBooleanDefaultValue(string? defaultValue)
        => string.Equals(defaultValue, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(defaultValue, "true", StringComparison.OrdinalIgnoreCase);

    private static bool HasValueLikeOptionName(string primaryOption)
        => GetOptionNameTokens(primaryOption)
            .Any(IsValueLikeOptionToken);

    private static IReadOnlyList<string> GetOptionNameTokens(string primaryOption)
    {
        var trimmed = primaryOption.TrimStart('-', '/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return [];
        }

        var separator = trimmed.IndexOfAny(['=', ':']);
        if (separator >= 0)
        {
            trimmed = trimmed[..separator];
        }

        return trimmed
            .Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(SplitCamelCaseTokens)
            .Where(token => token.Length > 0)
            .Select(token => token.ToLowerInvariant())
            .ToArray();
    }

    private static IEnumerable<string> SplitCamelCaseTokens(string token)
    {
        foreach (Match match in CamelCaseTokenRegex().Matches(token))
        {
            if (match.Length > 0)
            {
                yield return match.Value;
            }
        }
    }

    private static bool IsValueLikeOptionToken(string token)
    {
        if (ValueLikeOptionNameTokens.Contains(token))
        {
            return true;
        }

        if (TrySingularizeToken(token, out var singular) && ValueLikeOptionNameTokens.Contains(singular))
        {
            return true;
        }

        return ValueLikeOptionNameTokens.Any(suffix =>
            token.Length > suffix.Length
            && token.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TrySingularizeToken(string token, out string singular)
    {
        singular = string.Empty;
        if (token.Length <= 2)
        {
            return false;
        }

        if (token.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
        {
            singular = token[..^3] + "y";
            return true;
        }

        if (!token.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        singular = token[..^1];
        return singular.Length > 0;
    }

    private static bool IsLowSignalExplicitArgument(ToolHelpItem argument)
        => TryParseArgumentSignature(argument.Key, out var signature)
            && IsGenericArgumentName(signature.Name)
            && string.IsNullOrWhiteSpace(argument.Description);

    private static bool IsGenericArgumentName(string name)
        => GenericArgumentNames.Contains(name);

    private static string NormalizeUsageArgumentKey(string rawValue, bool isSequence)
    {
        var trimmed = rawValue.Trim();
        if (trimmed.EndsWith("...", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return isSequence ? $"{trimmed}..." : trimmed;
    }

    private static char? GetUsageArgumentQuantifier(string line, Match match)
    {
        var directQuantifier = TryReadUsageQuantifier(line, match.Index + match.Length);
        if (directQuantifier is not null)
        {
            return directQuantifier;
        }

        var groupStart = line.LastIndexOf('(', match.Index);
        if (groupStart < 0)
        {
            return null;
        }

        var groupEnd = line.IndexOf(')', match.Index + match.Length);
        if (groupEnd < 0 || groupStart >= match.Index || groupEnd < match.Index + match.Length)
        {
            return null;
        }

        return TryReadUsageQuantifier(line, groupEnd + 1);
    }

    private static char? TryReadUsageQuantifier(string line, int startIndex)
    {
        var index = startIndex;
        while (index < line.Length && char.IsWhiteSpace(line[index]))
        {
            index++;
        }

        if (index >= line.Length)
        {
            return null;
        }

        return line[index] is '*' or '+' or '?'
            ? line[index]
            : null;
    }

    private static ToolHelpItem? ExtractBareUsageArgument(string commandName, string commandPath, string line, bool hasChildCommands)
    {
        var remainder = StripUsageInvocationPrefix(commandName, commandPath, line);
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return null;
        }

        remainder = BareOptionClauseRegex().Replace(remainder, " ").Trim();
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return null;
        }

        var match = SingleBareUsageArgumentRegex().Match(remainder);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["name"].Value.Trim();
        if (value.Length == 0
            || value.All(char.IsDigit)
            || value.EndsWith(":", StringComparison.Ordinal)
            || LooksLikeOptionPlaceholder(value)
            || IsDispatcherPlaceholder(value)
            || string.Equals(value, "options", StringComparison.OrdinalIgnoreCase)
            || hasChildCommands)
        {
            return null;
        }

        var normalizedValue = NormalizeBareUsageArgumentValue(value);
        var isSequence = match.Groups["ellipsis"].Success || match.Groups["quantifier"].Value is "*" or "+";
        var isRequired = !match.Groups["optional"].Success && match.Groups["quantifier"].Value is not "*" and not "?";
        return new ToolHelpItem(
            NormalizeUsageArgumentKey(normalizedValue, isSequence),
            isRequired,
            null);
    }

    private static string StripUsageInvocationPrefix(string commandName, string commandPath, string line)
    {
        var invocation = string.IsNullOrWhiteSpace(commandPath)
            ? commandName
            : $"{commandName} {commandPath}";
        return line.StartsWith(invocation, StringComparison.OrdinalIgnoreCase)
            ? line[invocation.Length..].Trim()
            : line.Trim();
    }

    private static string NormalizeBareUsageArgumentValue(string value)
    {
        var trimmed = value.Trim('[', ']').Trim();
        if (trimmed.Contains('/') || trimmed.Contains('\\') || FileLikeUsageTokenRegex().IsMatch(trimmed))
        {
            return "FILE";
        }

        return trimmed;
    }

    private static bool LooksLikeWrappedUsageValueContinuation(string? previousLine, string currentLine)
    {
        if (string.IsNullOrWhiteSpace(previousLine))
        {
            return false;
        }

        var trimmedCurrent = currentLine.Trim();
        if (trimmedCurrent.Length == 0 || !SingleBareUsageArgumentRegex().Match(trimmedCurrent).Success)
        {
            return false;
        }

        var previousTrimmed = previousLine.TrimEnd();
        var matches = OptionTokenRegex().Matches(previousTrimmed);
        if (matches.Count == 0)
        {
            return false;
        }

        var trailing = previousTrimmed[(matches[^1].Index + matches[^1].Length)..].Trim();
        return trailing.Length == 0;
    }

    private static string StripBracketedPlaceholders(string key)
    {
        var result = BracketedPlaceholderRegex().Replace(key, string.Empty);
        return result;
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

    private static string? TryParseOptionToken(string segment, string? previousToken)
    {
        var trimmed = segment.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var match = OptionTokenRegex().Match(trimmed);
        if (match.Success && match.Index == 0)
        {
            return match.Value;
        }

        if (!PipeDelimitedOptionAliasSegmentRegex().IsMatch(trimmed))
        {
            return null;
        }

        if (previousToken?.StartsWith("/", StringComparison.Ordinal) == true)
        {
            return "/" + trimmed.TrimStart('-', '/');
        }

        return trimmed.Length == 1
            ? "-" + trimmed.TrimStart('-', '/')
            : "--" + trimmed.TrimStart('-', '/');
    }

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

        // Skip past quotes that may wrap the placeholder value
        if (index >= 0 && line[index] is '"' or '\'')
        {
            index--;
            while (index >= 0 && char.IsWhiteSpace(line[index]))
            {
                index--;
            }
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

    [GeneratedRegex(@"\[(?=[^\]]*(?:-|/))[^\]]+\]", RegexOptions.Compiled)]
    private static partial Regex BareOptionClauseRegex();

    [GeneratedRegex(@"^(?<optional>\[)?(?<name>[A-Za-z0-9][A-Za-z0-9._/\-\\:]*?)(?<ellipsis>\.\.\.)?(?(optional)\])(?<quantifier>[+*?])?$", RegexOptions.Compiled)]
    private static partial Regex SingleBareUsageArgumentRegex();

    [GeneratedRegex(@"\.[A-Za-z0-9]{1,8}$", RegexOptions.Compiled)]
    private static partial Regex FileLikeUsageTokenRegex();

    [GeneratedRegex(@"<[^>]*>|\[[^\]]*\]", RegexOptions.Compiled)]
    private static partial Regex BracketedPlaceholderRegex();

    [GeneratedRegex(@"[^A-Za-z0-9_\-]", RegexOptions.Compiled)]
    private static partial Regex InvalidArgumentTokenRegex();

    [GeneratedRegex(@"\d+$", RegexOptions.Compiled)]
    private static partial Regex TrailingDigitsRegex();

    [GeneratedRegex(@"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+", RegexOptions.Compiled)]
    private static partial Regex CamelCaseTokenRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_\.\?\-]*$", RegexOptions.Compiled)]
    private static partial Regex PipeDelimitedOptionAliasSegmentRegex();

    private sealed record OptionSignature(
        string? PrimaryName,
        IReadOnlyList<string> Aliases,
        string? ArgumentName,
        bool ArgumentRequired);

    private sealed record ArgumentSignature(
        string Name,
        bool IsSequence);
}
