using dnlib.DotNet;

internal static class StaticAnalysisHierarchySupport
{
    public static IEnumerable<PropertyDef> GetPropertiesFromHierarchy(TypeDef typeDef)
    {
        var chain = new Stack<TypeDef>();
        for (var current = typeDef; current is not null; current = ResolveBaseType(current))
        {
            chain.Push(current);
        }

        while (chain.Count > 0)
        {
            foreach (var property in chain.Pop().Properties)
            {
                yield return property;
            }
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
