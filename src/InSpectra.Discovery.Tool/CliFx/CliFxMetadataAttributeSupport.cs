using System.Reflection;

internal static class CliFxMetadataAttributeSupport
{
    public static T? GetNamedArgument<T>(CustomAttributeData attribute, string name)
    {
        var value = attribute.NamedArguments
            .FirstOrDefault(argument => string.Equals(argument.MemberName, name, StringComparison.Ordinal))
            .TypedValue
            .Value;
        return value is T typedValue ? typedValue : default;
    }
}
