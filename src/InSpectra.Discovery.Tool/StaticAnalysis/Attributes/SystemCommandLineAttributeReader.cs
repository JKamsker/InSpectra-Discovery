namespace InSpectra.Discovery.Tool.StaticAnalysis.Attributes;

using InSpectra.Discovery.Tool.StaticAnalysis.Models;

using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

using dnlib.DotNet;

/// <summary>
/// Basic reader for System.CommandLine tools. Since System.CommandLine is primarily
/// code-driven (new RootCommand(), new Command("name"), new Option&lt;T&gt;("--name")),
/// static attribute analysis is limited. This reader looks for classes that inherit
/// from System.CommandLine.Command/RootCommand and reads their properties decorated
/// with System.CommandLine.Binding.BinderBase or similar patterns.
///
/// Most of the CLI structure will come from the help crawl fallback.
/// </summary>
internal sealed class SystemCommandLineAttributeReader : IStaticAttributeReader
{
    private static readonly string[] CommandBaseTypeNames =
    [
        "System.CommandLine.Command",
        "System.CommandLine.RootCommand",
    ];

    public IReadOnlyDictionary<string, StaticCommandDefinition> Read(IReadOnlyList<ScannedModule> modules)
    {
        var commands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var scannedModule in modules)
        {
            foreach (var typeDef in scannedModule.Module.GetTypes())
            {
                if (!typeDef.IsClass || typeDef.IsAbstract || typeDef.IsInterface)
                {
                    continue;
                }

                if (!InheritsFromCommand(typeDef))
                {
                    continue;
                }

                var definition = ReadCommandType(typeDef);
                if (definition is null)
                {
                    continue;
                }

                var key = definition.Name ?? string.Empty;
                StaticCommandDefinitionSupport.UpsertBest(commands, key, definition);
            }
        }

        return commands;
    }

    private static StaticCommandDefinition? ReadCommandType(TypeDef typeDef)
    {
        var isRoot = IsRootCommand(typeDef);
        var name = isRoot ? null : typeDef.Name?.String?.Replace("Command", string.Empty).ToLowerInvariant();
        if (!isRoot && string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var options = new List<StaticOptionDefinition>();
        foreach (var field in typeDef.Fields)
        {
            if (!field.IsPublic && !field.IsFamily && !field.IsAssembly)
            {
                continue;
            }

            var fieldType = field.FieldSig?.Type;
            if (fieldType is null)
            {
                continue;
            }

            if (IsOptionType(fieldType))
            {
                var innerType = ExtractGenericArgument(fieldType);
                options.Add(new StaticOptionDefinition(
                    LongName: ConvertToKebabCase(StripSuffix(field.Name?.String, "Option")),
                    ShortName: null,
                    IsRequired: false,
                    IsSequence: StaticAnalysisTypeSupport.IsSequenceType(innerType),
                    IsBoolLike: StaticAnalysisTypeSupport.IsBoolType(innerType),
                    ClrType: StaticAnalysisTypeSupport.GetClrTypeName(innerType),
                    Description: null,
                    DefaultValue: null,
                    MetaValue: null,
                    AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(innerType),
                    PropertyName: field.Name?.String));
            }
        }

        foreach (var property in typeDef.Properties)
        {
            var propertyType = property.PropertySig?.RetType;
            if (propertyType is null)
            {
                continue;
            }

            if (IsOptionType(propertyType))
            {
                var innerType = ExtractGenericArgument(propertyType);
                options.Add(new StaticOptionDefinition(
                    LongName: ConvertToKebabCase(StripSuffix(property.Name?.String, "Option")),
                    ShortName: null,
                    IsRequired: false,
                    IsSequence: StaticAnalysisTypeSupport.IsSequenceType(innerType),
                    IsBoolLike: StaticAnalysisTypeSupport.IsBoolType(innerType),
                    ClrType: StaticAnalysisTypeSupport.GetClrTypeName(innerType),
                    Description: null,
                    DefaultValue: null,
                    MetaValue: null,
                    AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(innerType),
                    PropertyName: property.Name?.String));
            }
        }

        return new StaticCommandDefinition(
            Name: name,
            Description: null,
            IsDefault: isRoot,
            IsHidden: false,
            Values: [],
            Options: options.OrderBy(o => o.LongName).ToArray());
    }

    private static bool InheritsFromCommand(TypeDef typeDef)
    {
        for (var current = typeDef.BaseType; current is not null;)
        {
            var fullName = current.FullName;
            if (CommandBaseTypeNames.Any(n => string.Equals(fullName, n, StringComparison.Ordinal)))
            {
                return true;
            }

            var resolved = current.ResolveTypeDef();
            if (resolved is null)
            {
                break;
            }

            current = resolved.BaseType;
        }

        return false;
    }

    private static bool IsRootCommand(TypeDef typeDef)
    {
        for (var current = typeDef.BaseType; current is not null;)
        {
            if (string.Equals(current.FullName, "System.CommandLine.RootCommand", StringComparison.Ordinal))
            {
                return true;
            }

            var resolved = current.ResolveTypeDef();
            current = resolved?.BaseType;
        }

        return false;
    }

    private static bool IsOptionType(TypeSig? typeSig)
    {
        if (typeSig is GenericInstSig g)
        {
            var name = g.GenericType?.FullName?.Split('`')[0];
            return string.Equals(name, "System.CommandLine.Option", StringComparison.Ordinal);
        }

        for (var current = typeSig?.ToTypeDefOrRef(); current is not null;)
        {
            if (current.FullName.StartsWith("System.CommandLine.Option", StringComparison.Ordinal))
            {
                return true;
            }

            var resolved = current.ResolveTypeDef();
            current = resolved?.BaseType?.ResolveTypeDef();
        }

        return false;
    }

    private static TypeSig? ExtractGenericArgument(TypeSig? typeSig)
        => typeSig is GenericInstSig g && g.GenericArguments.Count > 0
            ? g.GenericArguments[0]
            : null;

    private static string? StripSuffix(string? name, string suffix)
    {
        if (name is null) return null;
        return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && name.Length > suffix.Length
            ? name[..^suffix.Length]
            : name;
    }

    private static string ConvertToKebabCase(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "value";
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0 && !char.IsUpper(name[i - 1])) sb.Append('-');
            sb.Append(char.ToLowerInvariant(name[i]));
        }

        return sb.ToString();
    }
}

