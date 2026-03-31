namespace InSpectra.Discovery.Tool.StaticAnalysis.Attributes;

using InSpectra.Discovery.Tool.StaticAnalysis.Models;

using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

using dnlib.DotNet;

/// <summary>
/// Reader for Argu (F#) CLI tools. Argu uses F# discriminated unions where each
/// case maps to a CLI argument. This reader looks for types inheriting from
/// Argu.IArgParserTemplate and extracts union cases as options. Since F# unions
/// are compiled to nested classes, extraction is approximate.
/// </summary>
internal sealed class ArguAttributeReader : IStaticAttributeReader
{
    private const string MandatoryAttributeName = "Argu.ArguAttributes+MandatoryAttribute";
    private const string MainCommandAttributeName = "Argu.ArguAttributes+MainCommandAttribute";
    private const string AltCommandLineAttributeName = "Argu.ArguAttributes+AltCommandLineAttribute";
    private const string CustomCommandLineAttributeName = "Argu.ArguAttributes+CustomCommandLineAttribute";
    private const string CliPrefixAttributeName = "Argu.ArguAttributes+CliPrefixAttribute";

    public IReadOnlyDictionary<string, StaticCommandDefinition> Read(IReadOnlyList<ScannedModule> modules)
    {
        var commands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var scannedModule in modules)
        {
            foreach (var typeDef in scannedModule.Module.GetTypes())
            {
                if (!typeDef.IsClass || typeDef.IsAbstract)
                {
                    continue;
                }

                if (!ImplementsIArgParserTemplate(typeDef))
                {
                    continue;
                }

                var options = ReadUnionCasesAsOptions(typeDef);
                if (options.Count == 0)
                {
                    continue;
                }

                var definition = new StaticCommandDefinition(
                    Name: null,
                    Description: null,
                    IsDefault: true,
                    IsHidden: false,
                    Values: [],
                    Options: options);

                var key = string.Empty;
                if (!commands.TryGetValue(key, out var existing) || options.Count > existing.Options.Count)
                {
                    commands[key] = definition;
                }
            }
        }

        return commands;
    }

    private static IReadOnlyList<StaticOptionDefinition> ReadUnionCasesAsOptions(TypeDef typeDef)
    {
        var options = new List<StaticOptionDefinition>();

        foreach (var nestedType in typeDef.NestedTypes)
        {
            if (!nestedType.IsNestedPublic || nestedType.IsAbstract)
            {
                continue;
            }

            var caseName = nestedType.Name?.String;
            if (string.IsNullOrWhiteSpace(caseName) || caseName.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(caseName, "Tags", StringComparison.Ordinal))
            {
                continue;
            }

            var longName = GetCustomName(nestedType) ?? ConvertToCliName(caseName);
            var isMandatory = HasAttribute(nestedType.CustomAttributes, MandatoryAttributeName);
            var altName = GetAltName(nestedType);

            options.Add(new StaticOptionDefinition(
                LongName: longName,
                ShortName: altName?.Length == 1 ? altName[0] : null,
                IsRequired: isMandatory,
                IsSequence: false,
                IsBoolLike: nestedType.Fields.Count(f => !f.IsSpecialName && !f.IsStatic) == 0,
                ClrType: null,
                Description: null,
                DefaultValue: null,
                MetaValue: null,
                AcceptedValues: [],
                PropertyName: caseName));
        }

        foreach (var field in typeDef.Fields)
        {
            if (field.IsSpecialName || field.IsStatic)
            {
                continue;
            }

            var caseName = field.Name?.String;
            if (string.IsNullOrWhiteSpace(caseName) || caseName.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            var longName = ConvertToCliName(caseName);
            options.Add(new StaticOptionDefinition(
                LongName: longName,
                ShortName: null,
                IsRequired: false,
                IsSequence: false,
                IsBoolLike: true,
                ClrType: null,
                Description: null,
                DefaultValue: null,
                MetaValue: null,
                AcceptedValues: [],
                PropertyName: caseName));
        }

        return options;
    }

    private static bool ImplementsIArgParserTemplate(TypeDef typeDef)
    {
        foreach (var iface in typeDef.Interfaces)
        {
            var ifaceName = iface.Interface?.FullName;
            if (ifaceName is not null && ifaceName.Contains("Argu", StringComparison.Ordinal))
            {
                return true;
            }
        }

        for (var current = typeDef.BaseType; current is not null;)
        {
            var fullName = current.FullName;
            if (fullName is not null && fullName.Contains("Argu", StringComparison.Ordinal))
            {
                return true;
            }

            var resolved = current.ResolveTypeDef();
            if (resolved is null) break;
            current = resolved.BaseType;
        }

        return false;
    }

    private static string? GetCustomName(TypeDef typeDef)
    {
        var attr = FindAttribute(typeDef.CustomAttributes, CustomCommandLineAttributeName);
        if (attr?.ConstructorArguments.Count > 0)
        {
            return attr.ConstructorArguments[0].Value is UTF8String u ? u.String
                : attr.ConstructorArguments[0].Value as string;
        }

        return null;
    }

    private static string? GetAltName(TypeDef typeDef)
    {
        var attr = FindAttribute(typeDef.CustomAttributes, AltCommandLineAttributeName);
        if (attr?.ConstructorArguments.Count > 0)
        {
            return attr.ConstructorArguments[0].Value is UTF8String u ? u.String
                : attr.ConstructorArguments[0].Value as string;
        }

        return null;
    }

    private static string ConvertToCliName(string caseName)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < caseName.Length; i++)
        {
            if (char.IsUpper(caseName[i]) && i > 0 && !char.IsUpper(caseName[i - 1]))
            {
                sb.Append('-');
            }

            sb.Append(char.ToLowerInvariant(caseName[i]));
        }

        return sb.ToString().Replace('_', '-');
    }

    private static bool HasAttribute(CustomAttributeCollection attributes, string fullName)
        => FindAttribute(attributes, fullName) is not null;

    private static CustomAttribute? FindAttribute(CustomAttributeCollection attributes, string fullName)
    {
        foreach (var attr in attributes)
            if (string.Equals(attr.AttributeType?.FullName, fullName, StringComparison.Ordinal))
                return attr;
        return null;
    }
}

