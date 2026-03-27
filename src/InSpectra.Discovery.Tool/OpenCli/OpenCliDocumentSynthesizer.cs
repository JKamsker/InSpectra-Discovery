using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static class OpenCliDocumentSynthesizer
{
    public static JsonObject ConvertFromXmldoc(XDocument xmlDocument, string title, string version = "1.0")
    {
        var rootCommands = new JsonArray();
        foreach (var command in GetElements(xmlDocument.Root, "Command"))
        {
            rootCommands.Add(ConvertCommand(command, []));
        }

        JsonNode? defaultOptions = null;
        foreach (var command in rootCommands.OfType<JsonObject>())
        {
            if (string.Equals(command["name"]?.GetValue<string>(), "__default_command", StringComparison.Ordinal))
            {
                defaultOptions = command["options"]?.DeepClone();
                break;
            }
        }

        return new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = title,
                ["version"] = version,
            },
            ["x-inspectra"] = new JsonObject
            {
                ["synthesized"] = true,
                ["artifactSource"] = "synthesized-from-xmldoc",
                ["sourceArtifact"] = "xmldoc.xml",
                ["generator"] = "InSpectra.Discovery",
            },
            ["options"] = defaultOptions,
            ["commands"] = rootCommands,
        };
    }

    private static JsonObject ConvertCommand(XElement commandNode, IReadOnlyList<string> parentPath)
    {
        var commandName = GetAttributeValue(commandNode, "Name") ?? string.Empty;
        var commandPath = parentPath.Concat([commandName]).ToArray();
        var command = new JsonObject
        {
            ["name"] = commandName,
        };

        var parametersNode = GetElements(commandNode, "Parameters").FirstOrDefault();
        var options = new JsonArray();
        foreach (var option in GetElements(parametersNode, "Option"))
        {
            var converted = ConvertOption(option);
            if (converted is not null)
            {
                options.Add(converted);
            }
        }

        if (options.Count > 0)
        {
            command["options"] = options;
        }

        var arguments = new JsonArray();
        foreach (var argument in GetElements(parametersNode, "Argument"))
        {
            arguments.Add(ConvertArgument(argument));
        }

        if (arguments.Count > 0)
        {
            command["arguments"] = arguments;
        }

        var description = GetDescriptionText(commandNode);
        if (!string.IsNullOrWhiteSpace(description))
        {
            command["description"] = description;
        }

        var children = new JsonArray();
        foreach (var child in GetElements(commandNode, "Command"))
        {
            children.Add(ConvertCommand(child, commandPath));
        }

        if (children.Count > 0)
        {
            command["commands"] = children;
        }

        command["hidden"] = string.Equals(commandName, "__default_command", StringComparison.Ordinal)
            || GetBoolean(commandNode, "Hidden", GetBoolean(commandNode, "IsHidden"));
        command["examples"] = ConvertExamples(commandNode, commandPath);
        return command;
    }

    private static JsonObject? ConvertOption(XElement optionNode)
    {
        var (name, aliases) = GetOptionAliases(GetAttributeValue(optionNode, "Short"), GetAttributeValue(optionNode, "Long"));
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var clrType = GetSimplifiedClrTypeName(GetAttributeValue(optionNode, "ClrType"));
        var option = new JsonObject
        {
            ["name"] = name,
            ["required"] = GetBoolean(optionNode, "Required"),
            ["recursive"] = GetBoolean(optionNode, "Recursive"),
            ["hidden"] = GetBoolean(optionNode, "Hidden", GetBoolean(optionNode, "IsHidden")),
        };

        if (aliases.Count > 0)
        {
            option["aliases"] = ToJsonArray(aliases);
        }

        var argument = CreateOptionArgument(clrType, GetAttributeValue(optionNode, "Kind"), GetAttributeValue(optionNode, "Value"));
        if (argument is not null)
        {
            option["arguments"] = new JsonArray { argument };
        }

        var description = GetDescriptionText(optionNode);
        if (!string.IsNullOrWhiteSpace(description))
        {
            option["description"] = description;
        }

        return option;
    }

    private static JsonObject ConvertArgument(XElement argumentNode)
    {
        var clrType = GetSimplifiedClrTypeName(GetAttributeValue(argumentNode, "ClrType"));
        var argument = new JsonObject
        {
            ["name"] = GetAttributeValue(argumentNode, "Name"),
            ["required"] = GetBoolean(argumentNode, "Required"),
            ["arity"] = new JsonObject
            {
                ["minimum"] = 1,
                ["maximum"] = 1,
            },
            ["hidden"] = GetBoolean(argumentNode, "Hidden", GetBoolean(argumentNode, "IsHidden")),
        };

        var description = GetDescriptionText(argumentNode);
        if (!string.IsNullOrWhiteSpace(description))
        {
            argument["description"] = description;
        }

        if (!string.IsNullOrWhiteSpace(clrType))
        {
            argument["metadata"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "ClrType",
                    ["value"] = clrType,
                }
            };
        }

        return argument;
    }

    private static JsonArray ConvertExamples(XElement commandNode, IReadOnlyList<string> commandPath)
    {
        var commandName = GetAttributeValue(commandNode, "Name") ?? string.Empty;
        if (string.Equals(commandName, "__default_command", StringComparison.Ordinal))
        {
            return [];
        }

        var tokens = GetElements(GetElements(commandNode, "Examples").FirstOrDefault(), "Example")
            .Select(example => GetAttributeValue(example, "commandLine") ?? GetAttributeValue(example, "CommandLine"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
        if (tokens.Length == 0)
        {
            return [];
        }

        var startSequence = commandPath.Count > 0 ? commandPath.ToArray() : [commandName];
        var examples = new JsonArray();
        var index = 0;

        while (index < tokens.Length)
        {
            var parts = new List<string>();
            if (StartsWithSequence(tokens, index, startSequence))
            {
                parts.AddRange(startSequence);
                index += startSequence.Length;
            }
            else
            {
                parts.Add(tokens[index]);
                index++;
            }

            while (index < tokens.Length && !StartsWithSequence(tokens, index, startSequence))
            {
                parts.Add(tokens[index]);
                index++;
            }

            var example = string.Join(" ", parts).Trim();
            if (!string.IsNullOrWhiteSpace(example))
            {
                examples.Add(example);
            }
        }

        return examples;
    }

    private static bool StartsWithSequence(IReadOnlyList<string> tokens, int index, IReadOnlyList<string> sequence)
    {
        if (sequence.Count == 0 || index + sequence.Count > tokens.Count)
        {
            return false;
        }

        for (var offset = 0; offset < sequence.Count; offset++)
        {
            if (!string.Equals(tokens[index + offset], sequence[offset], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static JsonObject? CreateOptionArgument(string? clrType, string? kind, string? value)
    {
        var isNullableBool = string.Equals(clrType, "System.Nullable<System.Boolean>", StringComparison.Ordinal) ||
            (clrType?.StartsWith("System.Nullable<System.Boolean>", StringComparison.Ordinal) ?? false);
        var needsArgument = !string.Equals(kind, "flag", StringComparison.OrdinalIgnoreCase) || isNullableBool;
        if (!needsArgument)
        {
            return null;
        }

        var argumentName = string.IsNullOrWhiteSpace(value) || string.Equals(value, "NULL", StringComparison.Ordinal) ? "VALUE" : value;
        return new JsonObject
        {
            ["name"] = argumentName,
            ["required"] = true,
            ["arity"] = new JsonObject
            {
                ["minimum"] = 1,
                ["maximum"] = 1,
            },
            ["metadata"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "ClrType",
                    ["value"] = clrType,
                }
            },
        };
    }

    private static (string? Name, List<string> Aliases) GetOptionAliases(string? shortValue, string? longValue)
    {
        var longParts = SplitAliases(longValue);
        var shortParts = SplitAliases(shortValue);
        string? primaryName = null;
        var aliases = new List<string>();

        if (longParts.Count > 0)
        {
            primaryName = "--" + longParts[0];
            aliases.AddRange(longParts.Skip(1).Select(alias => "--" + alias));
            aliases.AddRange(shortParts.Select(alias => "-" + alias));
        }
        else if (shortParts.Count > 0)
        {
            primaryName = "-" + shortParts[0];
            aliases.AddRange(shortParts.Skip(1).Select(alias => "-" + alias));
        }

        return (primaryName, aliases);
    }

    private static List<string> SplitAliases(string? value)
        => (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

    private static bool GetBoolean(XElement? node, string attributeName, bool defaultValue = false)
    {
        var value = GetAttributeValue(node, attributeName);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetDescriptionText(XElement? node)
    {
        var descriptionNode = GetElements(node, "Description").FirstOrDefault();
        var value = descriptionNode?.Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? GetSimplifiedClrTypeName(string? clrType)
    {
        if (string.IsNullOrWhiteSpace(clrType))
        {
            return null;
        }

        var trimmed = clrType.Trim();
        var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^System\.Nullable`1\[\[(?<inner>.+)\]\]$");
        if (match.Success)
        {
            var inner = match.Groups["inner"].Value;
            var commaIndex = inner.IndexOf(',');
            var innerName = commaIndex >= 0 ? inner[..commaIndex] : inner;
            return $"System.Nullable<{innerName}>";
        }

        return trimmed;
    }

    private static string? GetAttributeValue(XElement? element, string name)
        => element?.Attributes().FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static IEnumerable<XElement> GetElements(XElement? element, string localName)
        => element?.Elements().Where(child => string.Equals(child.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase)) ?? [];

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }
}
