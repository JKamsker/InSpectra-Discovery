namespace InSpectra.Discovery.Tool.StaticAnalysis;

using dnlib.DotNet;

internal static class StaticAnalysisTypeSupport
{
    public static IReadOnlyList<string> GetAcceptedValues(TypeSig? typeSig)
    {
        var resolvedType = UnwrapNullable(typeSig)?.ToTypeDefOrRef()?.ResolveTypeDef();
        if (resolvedType is null || !resolvedType.IsEnum)
        {
            return [];
        }

        return resolvedType.Fields
            .Where(field => !field.IsSpecialName && field.IsStatic)
            .Select(field => field.Name.String)
            .ToArray();
    }

    public static string? GetClrTypeName(TypeSig? typeSig)
    {
        if (typeSig is null)
        {
            return null;
        }

        if (typeSig is SZArraySig arraySig)
        {
            return $"{GetClrTypeName(arraySig.Next)}[]";
        }

        if (typeSig is GenericInstSig genericInstSig)
        {
            var genericName = genericInstSig.GenericType?.FullName?.Split('`')[0];
            if (string.Equals(genericName, "System.Nullable", StringComparison.Ordinal)
                && genericInstSig.GenericArguments.Count == 1)
            {
                return $"System.Nullable<{GetClrTypeName(genericInstSig.GenericArguments[0])}>";
            }

            var genericArguments = string.Join(", ", genericInstSig.GenericArguments.Select(GetClrTypeName));
            return $"{genericName}<{genericArguments}>";
        }

        return typeSig.FullName;
    }

    public static bool IsBoolType(TypeSig? typeSig)
        => string.Equals(UnwrapNullable(typeSig)?.FullName, "System.Boolean", StringComparison.Ordinal);

    public static bool IsSequenceType(TypeSig? typeSig)
    {
        if (typeSig is SZArraySig)
        {
            return true;
        }

        if (string.Equals(typeSig?.FullName, "System.String", StringComparison.Ordinal))
        {
            return false;
        }

        if (typeSig is GenericInstSig genericInstSig)
        {
            var genericName = genericInstSig.GenericType?.FullName?.Split('`')[0];
            if (string.Equals(genericName, "System.Nullable", StringComparison.Ordinal))
            {
                return false;
            }

            return genericName is "System.Collections.Generic.IEnumerable"
                or "System.Collections.Generic.IList"
                or "System.Collections.Generic.ICollection"
                or "System.Collections.Generic.IReadOnlyList"
                or "System.Collections.Generic.IReadOnlyCollection"
                or "System.Collections.Generic.List"
                or "System.Collections.Generic.HashSet"
                or "System.Collections.Generic.ISet";
        }

        return false;
    }

    private static TypeSig? UnwrapNullable(TypeSig? typeSig)
        => typeSig is GenericInstSig genericInstSig
            && string.Equals(genericInstSig.GenericType?.FullName?.Split('`')[0], "System.Nullable", StringComparison.Ordinal)
            && genericInstSig.GenericArguments.Count == 1
            ? genericInstSig.GenericArguments[0]
            : typeSig;
}

