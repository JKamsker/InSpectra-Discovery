using System.Reflection;

internal static class CliFxMetadataTypeSupport
{
    public static bool IsRequired(PropertyInfo property)
        => property.CustomAttributes.Any(attribute => string.Equals(attribute.AttributeType.FullName, "System.Runtime.CompilerServices.RequiredMemberAttribute", StringComparison.Ordinal));

    public static bool IsSequence(PropertyInfo property, CustomAttributeData attribute)
    {
        var converterType = CliFxMetadataAttributeSupport.GetNamedArgument<Type>(attribute, "Converter");
        if (converterType is not null)
        {
            return converterType
                .GetInterfaces()
                .Concat(GetBaseTypes(converterType))
                .Any(type => string.Equals(type.FullName?.Split('`')[0], "CliFx.Activation.SequenceInputConverter", StringComparison.Ordinal));
        }

        return IsSequenceType(property.PropertyType);
    }

    public static string? GetClrTypeName(Type type)
    {
        if (type.IsArray)
        {
            return $"{GetClrTypeName(type.GetElementType()!)}[]";
        }

        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            return $"System.Nullable<{GetClrTypeName(nullableType)}>";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var genericName = type.GetGenericTypeDefinition().FullName?.Split('`')[0] ?? type.Name;
        var genericArguments = string.Join(", ", type.GetGenericArguments().Select(GetClrTypeName));
        return $"{genericName}<{genericArguments}>";
    }

    public static IReadOnlyList<string> GetAcceptedValues(Type type)
    {
        var enumType = Nullable.GetUnderlyingType(type) ?? type;
        return enumType.IsEnum ? Enum.GetNames(enumType) : [];
    }

    private static IEnumerable<Type> GetBaseTypes(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    private static bool IsSequenceType(Type type)
    {
        if (type.IsArray)
        {
            return true;
        }

        if (string.Equals(type.FullName, typeof(string).FullName, StringComparison.Ordinal))
        {
            return false;
        }

        return type.GetInterfaces().Any(interfaceType =>
            string.Equals(interfaceType.FullName?.Split('`')[0], "System.Collections.Generic.IEnumerable", StringComparison.Ordinal));
    }
}
