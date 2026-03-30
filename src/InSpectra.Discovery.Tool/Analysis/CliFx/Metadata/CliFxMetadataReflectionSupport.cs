namespace InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;

using System.Reflection;

internal static class CliFxMetadataReflectionSupport
{
    public static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }

    public static IEnumerable<PropertyInfo> GetPublicInstanceProperties(Type type)
    {
        var chain = new Stack<Type>();
        for (var current = type; current is not null && !string.Equals(current.FullName, typeof(object).FullName, StringComparison.Ordinal); current = current.BaseType)
        {
            chain.Push(current);
        }

        while (chain.Count > 0)
        {
            foreach (var property in chain.Pop().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                yield return property;
            }
        }
    }

    public static bool ReferencesCliFx(Assembly assembly)
        => string.Equals(assembly.GetName().Name, "CliFx", StringComparison.OrdinalIgnoreCase)
            || assembly.GetReferencedAssemblies().Any(reference => string.Equals(reference.Name, "CliFx", StringComparison.OrdinalIgnoreCase));

    public static CustomAttributeData? FindAttribute(IEnumerable<CustomAttributeData> attributes, IEnumerable<string> fullNames)
        => attributes.FirstOrDefault(attribute => fullNames.Any(fullName =>
            string.Equals(attribute.AttributeType.FullName, fullName, StringComparison.Ordinal)));
}

