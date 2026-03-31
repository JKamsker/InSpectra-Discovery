namespace InSpectra.Discovery.Tool.StaticAnalysis.Attributes;

using dnlib.DotNet;

internal static class StaticAnalysisAttributeSupport
{
    public static CustomAttribute? FindAttribute(CustomAttributeCollection attributes, string fullName)
    {
        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute.AttributeType?.FullName, fullName, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    public static CustomAttribute? FindAttribute(CustomAttributeCollection attributes, IReadOnlyList<string> fullNames)
    {
        foreach (var attribute in attributes)
        {
            var attributeFullName = attribute.AttributeType?.FullName;
            if (attributeFullName is null)
            {
                continue;
            }

            foreach (var fullName in fullNames)
            {
                if (string.Equals(attributeFullName, fullName, StringComparison.Ordinal))
                {
                    return attribute;
                }
            }
        }

        return null;
    }

    public static string? GetAttributeString(CustomAttributeCollection attributes, string attributeName)
        => GetConstructorArgumentString(FindAttribute(attributes, attributeName), 0);

    public static string? GetConstructorArgumentString(CustomAttribute? attribute, int index)
    {
        if (attribute is null || index >= attribute.ConstructorArguments.Count)
        {
            return null;
        }

        return attribute.ConstructorArguments[index].Value is UTF8String utf8String
            ? utf8String.String
            : attribute.ConstructorArguments[index].Value as string;
    }

    public static int GetConstructorArgumentInt(CustomAttribute? attribute, int index, int fallback = 0)
    {
        if (attribute is null || index >= attribute.ConstructorArguments.Count)
        {
            return fallback;
        }

        return attribute.ConstructorArguments[index].Value is int intValue
            ? intValue
            : fallback;
    }

    public static string? GetConstructorArgumentValueAsString(CustomAttribute? attribute, int index)
    {
        if (attribute is null || index >= attribute.ConstructorArguments.Count)
        {
            return null;
        }

        return attribute.ConstructorArguments[index].Value?.ToString();
    }

    public static string? GetNamedArgumentString(CustomAttribute? attribute, string name)
    {
        if (attribute is null)
        {
            return null;
        }

        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Name?.String, name, StringComparison.Ordinal))
            {
                return namedArgument.Value is UTF8String utf8String
                    ? utf8String.String
                    : namedArgument.Value as string;
            }
        }

        return null;
    }

    public static int GetNamedArgumentInt(CustomAttribute? attribute, string name, int fallback = 0)
    {
        if (attribute is null)
        {
            return fallback;
        }

        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Name?.String, name, StringComparison.Ordinal)
                && namedArgument.Value is int intValue)
            {
                return intValue;
            }
        }

        return fallback;
    }

    public static bool GetNamedArgumentBool(CustomAttribute? attribute, string name)
    {
        if (attribute is null)
        {
            return false;
        }

        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Name?.String, name, StringComparison.Ordinal))
            {
                return namedArgument.Value is bool boolValue && boolValue;
            }
        }

        return false;
    }

    public static string? GetNamedArgumentValueAsString(CustomAttribute? attribute, string name)
    {
        if (attribute is null)
        {
            return null;
        }

        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Name?.String, name, StringComparison.Ordinal))
            {
                return namedArgument.Value?.ToString();
            }
        }

        return null;
    }

    public static string[] GetNamedArgumentStrings(CustomAttribute? attribute, string name)
    {
        if (attribute is null)
        {
            return [];
        }

        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (!string.Equals(namedArgument.Name?.String, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (namedArgument.Value is IList<CAArgument> values)
            {
                return values
                    .Select(value => value.Value is UTF8String utf8String ? utf8String.String : value.Value?.ToString() ?? string.Empty)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToArray();
            }
        }

        return [];
    }
}

