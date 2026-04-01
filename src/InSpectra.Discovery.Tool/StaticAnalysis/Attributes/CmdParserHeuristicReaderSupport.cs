namespace InSpectra.Discovery.Tool.StaticAnalysis.Attributes;

using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;
using InSpectra.Discovery.Tool.StaticAnalysis.Models;

using dnlib.DotNet;

internal static class CmdParserHeuristicReaderSupport
{
    public static StaticCommandDefinition? TryReadBestCandidate(IReadOnlyList<ScannedModule> modules)
    {
        StaticCommandDefinition? bestDefinition = null;
        var bestScore = int.MinValue;

        foreach (var typeDef in modules.SelectMany(static module => module.Module.GetTypes()))
        {
            if (!LooksLikeCandidate(typeDef))
            {
                continue;
            }

            var definition = ReadDefinition(typeDef);
            if (definition is null)
            {
                continue;
            }

            var score = ScoreCandidate(typeDef, definition);
            if (score > bestScore)
            {
                bestScore = score;
                bestDefinition = definition;
            }
        }

        return bestDefinition;
    }

    internal static StaticCommandDefinition? ReadDefinition(TypeDef typeDef)
    {
        var options = new List<StaticOptionDefinition>();
        foreach (var property in StaticAnalysisHierarchySupport.GetPropertiesFromHierarchy(typeDef))
        {
            if (!LooksLikeHeuristicProperty(property, out var propertyType))
            {
                continue;
            }

            options.Add(BuildOptionDefinition(property.Name?.String, propertyType));
        }

        foreach (var field in StaticAnalysisHierarchySupport.GetFieldsFromHierarchy(typeDef))
        {
            if (!LooksLikeHeuristicField(field, out var fieldType))
            {
                continue;
            }

            options.Add(BuildOptionDefinition(field.Name?.String, fieldType));
        }

        var deduplicatedOptions = options
            .GroupBy(static option => option.LongName ?? option.PropertyName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderByDescending(static option => option.IsRequired)
            .ThenBy(static option => option.LongName)
            .ToArray();
        if (deduplicatedOptions.Length == 0)
        {
            return null;
        }

        return new StaticCommandDefinition(
            Name: null,
            Description: null,
            IsDefault: true,
            IsHidden: false,
            Values: [],
            Options: deduplicatedOptions);
    }

    internal static string NormalizeOptionName(string? memberName)
        => ConvertToKebabCase(TrimKnownSuffix(memberName));

    private static bool LooksLikeCandidate(TypeDef typeDef)
    {
        if (!typeDef.IsClass || typeDef.IsAbstract || typeDef.IsInterface)
        {
            return false;
        }

        var typeName = typeDef.Name?.String;
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        return typeName.EndsWith("Options", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("ParseOptions", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Args", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Arguments", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeHeuristicProperty(PropertyDef property, out TypeSig? propertyType)
    {
        propertyType = property.PropertySig?.RetType;
        if (propertyType is null
            || property.GetMethod is null && property.SetMethod is null
            || property.GetMethod?.IsStatic == true
            || property.SetMethod?.IsStatic == true)
        {
            return false;
        }

        return IsUsefulHeuristicMember(property.Name?.String, propertyType);
    }

    private static bool LooksLikeHeuristicField(FieldDef field, out TypeSig? fieldType)
    {
        fieldType = field.FieldSig?.Type;
        if (field.IsStatic || field.IsSpecialName || field.Name?.String.Contains("k__BackingField", StringComparison.Ordinal) == true)
        {
            return false;
        }

        return IsUsefulHeuristicMember(field.Name?.String, fieldType);
    }

    private static bool IsUsefulHeuristicMember(string? memberName, TypeSig? typeSig)
    {
        if (string.IsNullOrWhiteSpace(memberName)
            || memberName.StartsWith("_", StringComparison.Ordinal)
            || memberName.Contains('<', StringComparison.Ordinal))
        {
            return false;
        }

        return IsSimpleValueType(typeSig) || StaticAnalysisTypeSupport.IsSequenceType(typeSig);
    }

    private static bool IsSimpleValueType(TypeSig? typeSig)
    {
        var effectiveType = UnwrapNullable(typeSig);
        if (effectiveType is null)
        {
            return false;
        }

        if (StaticAnalysisTypeSupport.GetAcceptedValues(effectiveType).Count > 0)
        {
            return true;
        }

        return effectiveType.FullName is "System.String"
            or "System.Boolean"
            or "System.Char"
            or "System.Byte"
            or "System.SByte"
            or "System.Int16"
            or "System.UInt16"
            or "System.Int32"
            or "System.UInt32"
            or "System.Int64"
            or "System.UInt64"
            or "System.Single"
            or "System.Double"
            or "System.Decimal"
            or "System.Guid"
            or "System.DateTime"
            or "System.DateTimeOffset"
            or "System.TimeSpan"
            or "System.Uri";
    }

    private static TypeSig? UnwrapNullable(TypeSig? typeSig)
        => typeSig is GenericInstSig genericInstSig
            && string.Equals(genericInstSig.GenericType?.FullName?.Split('`')[0], "System.Nullable", StringComparison.Ordinal)
            && genericInstSig.GenericArguments.Count == 1
            ? genericInstSig.GenericArguments[0]
            : typeSig;

    private static StaticOptionDefinition BuildOptionDefinition(string? memberName, TypeSig? typeSig)
    {
        var normalizedName = ConvertToKebabCase(TrimKnownSuffix(memberName));
        return new StaticOptionDefinition(
            LongName: normalizedName,
            ShortName: null,
            IsRequired: false,
            IsSequence: StaticAnalysisTypeSupport.IsSequenceType(typeSig),
            IsBoolLike: StaticAnalysisTypeSupport.IsBoolType(typeSig),
            ClrType: StaticAnalysisTypeSupport.GetClrTypeName(typeSig),
            Description: null,
            DefaultValue: null,
            MetaValue: null,
            AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(typeSig),
            PropertyName: memberName);
    }

    private static int ScoreCandidate(TypeDef typeDef, StaticCommandDefinition definition)
    {
        var score = definition.Options.Count * 100;
        var typeName = typeDef.Name?.String ?? string.Empty;
        if (typeName.EndsWith("ParseOptions", StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
        }
        else if (typeName.EndsWith("Options", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        var typeNamespace = typeDef.Namespace?.String;
        if (!string.IsNullOrWhiteSpace(typeNamespace) && typeNamespace.Contains(".Options", StringComparison.Ordinal))
        {
            score += 10;
        }

        return score;
    }

    private static string TrimKnownSuffix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "value";
        }

        if (value.EndsWith("Option", StringComparison.OrdinalIgnoreCase) && value.Length > "Option".Length)
        {
            return value[..^"Option".Length];
        }

        if (value.EndsWith("Options", StringComparison.OrdinalIgnoreCase) && value.Length > "Options".Length)
        {
            return value[..^"Options".Length];
        }

        return value;
    }

    private static string ConvertToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "value";
        }

        var builder = new System.Text.StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '_')
            {
                builder.Append('-');
                continue;
            }

            if (char.IsUpper(character)
                && index > 0
                && !char.IsUpper(value[index - 1])
                && value[index - 1] != '-')
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
