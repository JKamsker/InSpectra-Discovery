namespace InSpectra.Discovery.Tool.OpenCli.Xmldoc;

using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static class OpenCliXmldocInputBuilder
{
    public static JsonArray ConvertOptions(XElement? parametersNode)
    {
        var options = new JsonArray();
        foreach (var option in OpenCliXmldocSupport.GetElements(parametersNode, "Option"))
        {
            var converted = ConvertOption(option);
            if (converted is not null)
            {
                options.Add(converted);
            }
        }

        return options;
    }

    public static JsonArray ConvertArguments(XElement? parametersNode)
    {
        var arguments = new JsonArray();
        foreach (var argument in OpenCliXmldocSupport.GetElements(parametersNode, "Argument"))
        {
            arguments.Add(ConvertArgument(argument));
        }

        return arguments;
    }

    private static JsonObject? ConvertOption(XElement optionNode)
    {
        var (name, aliases) = GetOptionAliases(
            OpenCliXmldocSupport.GetAttributeValue(optionNode, "Short"),
            OpenCliXmldocSupport.GetAttributeValue(optionNode, "Long"));
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var clrType = OpenCliXmldocSupport.GetSimplifiedClrTypeName(
            OpenCliXmldocSupport.GetAttributeValue(optionNode, "ClrType"));
        var option = new JsonObject
        {
            ["name"] = name,
            ["recursive"] = OpenCliXmldocSupport.GetBoolean(optionNode, "Recursive"),
            ["hidden"] = OpenCliXmldocSupport.GetBoolean(
                optionNode,
                "Hidden",
                OpenCliXmldocSupport.GetBoolean(optionNode, "IsHidden")),
        };

        if (aliases.Count > 0)
        {
            option["aliases"] = OpenCliXmldocSupport.ToJsonArray(aliases);
        }

        var argument = CreateOptionArgument(
            clrType,
            OpenCliXmldocSupport.GetAttributeValue(optionNode, "Kind"),
            OpenCliXmldocSupport.GetAttributeValue(optionNode, "Value"));
        if (argument is not null)
        {
            option["arguments"] = new JsonArray { argument };
        }

        var description = OpenCliXmldocSupport.GetDescriptionText(optionNode);
        if (!string.IsNullOrWhiteSpace(description))
        {
            option["description"] = description;
        }

        return option;
    }

    private static JsonObject ConvertArgument(XElement argumentNode)
    {
        var clrType = OpenCliXmldocSupport.GetSimplifiedClrTypeName(
            OpenCliXmldocSupport.GetAttributeValue(argumentNode, "ClrType"));
        var required = OpenCliXmldocSupport.GetBoolean(argumentNode, "Required");
        var argument = new JsonObject
        {
            ["name"] = OpenCliXmldocSupport.NormalizeArgumentName(
                OpenCliXmldocSupport.GetAttributeValue(argumentNode, "Name")),
            ["required"] = required,
            ["arity"] = OpenCliXmldocSupport.BuildArity(
                required ? 1 : 0,
                OpenCliXmldocSupport.IsVectorKind(OpenCliXmldocSupport.GetAttributeValue(argumentNode, "Kind"))),
            ["hidden"] = OpenCliXmldocSupport.GetBoolean(
                argumentNode,
                "Hidden",
                OpenCliXmldocSupport.GetBoolean(argumentNode, "IsHidden")),
        };

        var description = OpenCliXmldocSupport.GetDescriptionText(argumentNode);
        if (!string.IsNullOrWhiteSpace(description))
        {
            argument["description"] = description;
        }

        ApplyClrTypeMetadata(argument, clrType);
        return argument;
    }

    private static JsonObject? CreateOptionArgument(string? clrType, string? kind, string? value)
    {
        var isNullableBool = string.Equals(clrType, "System.Nullable<System.Boolean>", StringComparison.Ordinal)
            || (clrType?.StartsWith("System.Nullable<System.Boolean>", StringComparison.Ordinal) ?? false);
        var needsArgument = !string.Equals(kind, "flag", StringComparison.OrdinalIgnoreCase) || isNullableBool;
        if (!needsArgument)
        {
            return null;
        }

        var argumentName = string.IsNullOrWhiteSpace(value) || string.Equals(value, "NULL", StringComparison.Ordinal)
            ? "VALUE"
            : value;
        var argument = new JsonObject
        {
            ["name"] = argumentName,
            ["required"] = true,
            ["arity"] = OpenCliXmldocSupport.BuildArity(1, OpenCliXmldocSupport.IsVectorKind(kind)),
        };

        ApplyClrTypeMetadata(argument, clrType);
        return argument;
    }

    private static void ApplyClrTypeMetadata(JsonObject node, string? clrType)
    {
        if (string.IsNullOrWhiteSpace(clrType))
        {
            return;
        }

        node["metadata"] = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "ClrType",
                ["value"] = clrType,
            }
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
}

