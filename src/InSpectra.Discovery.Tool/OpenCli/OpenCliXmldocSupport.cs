namespace InSpectra.Discovery.Tool.OpenCli;

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static partial class OpenCliXmldocSupport
{
    public static string NormalizeCommandName(XElement commandNode)
    {
        var commandName = GetAttributeValue(commandNode, "Name")?.Trim();
        return GetBoolean(commandNode, "IsDefault") || string.IsNullOrWhiteSpace(commandName)
            ? "__default_command"
            : commandName;
    }

    public static bool IsDefaultCommand(XElement commandNode)
        => string.Equals(NormalizeCommandName(commandNode), "__default_command", StringComparison.Ordinal);

    public static string NormalizeArgumentName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "value";
        }

        var normalized = NonAlphaNumericArgumentNameRegex().Replace(value.Trim(), "-")
            .Trim('-')
            .ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? "value" : normalized;
    }

    public static string? GetDescriptionText(XElement? node)
    {
        var descriptionNode = GetElements(node, "Description").FirstOrDefault();
        var value = descriptionNode?.Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static bool IsVectorKind(string? kind)
        => string.Equals(kind?.Trim(), "vector", StringComparison.OrdinalIgnoreCase);

    public static string? GetSimplifiedClrTypeName(string? clrType)
    {
        if (string.IsNullOrWhiteSpace(clrType))
        {
            return null;
        }

        var trimmed = clrType.Trim();
        var match = NullableClrTypeRegex().Match(trimmed);
        if (match.Success)
        {
            var inner = match.Groups["inner"].Value;
            var commaIndex = inner.IndexOf(',');
            var innerName = commaIndex >= 0 ? inner[..commaIndex] : inner;
            return $"System.Nullable<{innerName}>";
        }

        return trimmed;
    }

    public static bool GetBoolean(XElement? node, string attributeName, bool defaultValue = false)
    {
        var value = GetAttributeValue(node, attributeName);
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetAttributeValue(XElement? element, string name)
        => element?.Attributes()
            .FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    public static IEnumerable<XElement> GetElements(XElement? element, string localName)
        => element?.Elements()
            .Where(child => string.Equals(child.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
           ?? [];

    public static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    public static JsonObject BuildArity(int minimum, bool isVector)
    {
        var arity = new JsonObject
        {
            ["minimum"] = minimum,
        };

        if (!isVector)
        {
            arity["maximum"] = 1;
        }

        return arity;
    }

    public static void AddIfPresent(JsonObject target, string propertyName, JsonNode? value)
    {
        if (value is not null)
        {
            target[propertyName] = value;
        }
    }

    [GeneratedRegex(@"[^A-Za-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumericArgumentNameRegex();

    [GeneratedRegex(@"^System\.Nullable`1\[\[(?<inner>.+)\]\]$", RegexOptions.Compiled)]
    private static partial Regex NullableClrTypeRegex();
}

