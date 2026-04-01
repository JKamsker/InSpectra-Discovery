namespace InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

using dnlib.DotNet;

internal static class StaticAnalysisHierarchySupport
{
    public static IEnumerable<PropertyDef> GetPropertiesFromHierarchy(TypeDef typeDef)
    {
        foreach (var current in EnumerateHierarchy(typeDef))
        {
            foreach (var property in current.Properties)
            {
                yield return property;
            }
        }
    }

    public static IEnumerable<FieldDef> GetFieldsFromHierarchy(TypeDef typeDef)
    {
        foreach (var current in EnumerateHierarchy(typeDef))
        {
            foreach (var field in current.Fields)
            {
                yield return field;
            }
        }
    }

    private static IEnumerable<TypeDef> EnumerateHierarchy(TypeDef typeDef)
    {
        var chain = new Stack<TypeDef>();
        for (var current = typeDef; current is not null; current = ResolveBaseType(current))
        {
            chain.Push(current);
        }

        while (chain.Count > 0)
        {
            yield return chain.Pop();
        }
    }

    private static TypeDef? ResolveBaseType(TypeDef typeDef)
    {
        var baseTypeRef = typeDef.BaseType;
        if (baseTypeRef is null || string.Equals(baseTypeRef.FullName, "System.Object", StringComparison.Ordinal))
        {
            return null;
        }

        return baseTypeRef.ResolveTypeDef();
    }
}
