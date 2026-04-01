namespace InSpectra.Discovery.Tool.StaticAnalysis.Attributes;

using InSpectra.Discovery.Tool.StaticAnalysis.Models;

using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

using dnlib.DotNet;

internal sealed class CmdParserAttributeReader : IStaticAttributeReader
{
    private const string VerbAttributeName = "CommandLine.VerbAttribute";
    private const string OptionAttributeName = "CommandLine.OptionAttribute";
    private const string ValueAttributeName = "CommandLine.ValueAttribute";

    public IReadOnlyDictionary<string, StaticCommandDefinition> Read(IReadOnlyList<ScannedModule> modules)
    {
        var commands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase);
        var hasAnyVerb = false;
        var verblessTypes = new List<TypeDef>();

        foreach (var scannedModule in modules)
        {
            foreach (var typeDef in GetAllTypes(scannedModule.Module))
            {
                if (!typeDef.IsClass || typeDef.IsAbstract || typeDef.IsInterface)
                {
                    continue;
                }

                var verbAttribute = StaticAnalysisAttributeSupport.FindAttribute(typeDef.CustomAttributes, VerbAttributeName);
                if (verbAttribute is not null)
                {
                    hasAnyVerb = true;
                    var definition = ReadVerbDefinition(typeDef, verbAttribute);
                    StaticCommandDefinitionSupport.UpsertBest(commands, definition);
                }
                else if (HasOptionOrValueProperties(typeDef))
                {
                    verblessTypes.Add(typeDef);
                }
            }
        }

        if (!hasAnyVerb)
        {
            foreach (var typeDef in verblessTypes)
            {
                var definition = ReadVerblessDefinition(typeDef);
                StaticCommandDefinitionSupport.UpsertBest(commands, string.Empty, definition);
            }
        }

        if (commands.Count == 0)
        {
            var heuristicDefinition = CmdParserHeuristicReaderSupport.TryReadBestCandidate(modules);
            if (heuristicDefinition is not null)
            {
                StaticCommandDefinitionSupport.UpsertBest(commands, string.Empty, heuristicDefinition);
            }
        }

        return commands;
    }

    private static StaticCommandDefinition ReadVerbDefinition(TypeDef typeDef, CustomAttribute verbAttribute)
    {
        var name = StaticAnalysisAttributeSupport.GetConstructorArgumentString(verbAttribute, 0);
        var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(verbAttribute, "HelpText");
        var isDefault = StaticAnalysisAttributeSupport.GetNamedArgumentBool(verbAttribute, "IsDefault");
        var isHidden = StaticAnalysisAttributeSupport.GetNamedArgumentBool(verbAttribute, "Hidden");
        var (options, values) = ReadPropertiesFromTypeHierarchy(typeDef);

        return new StaticCommandDefinition(
            Name: name,
            Description: description,
            IsDefault: isDefault,
            IsHidden: isHidden,
            Values: values.OrderBy(v => v.Index).ToArray(),
            Options: options.OrderByDescending(o => o.IsRequired).ThenBy(o => o.LongName).ThenBy(o => o.ShortName).ToArray());
    }

    private static StaticCommandDefinition ReadVerblessDefinition(TypeDef typeDef)
    {
        var (options, values) = ReadPropertiesFromTypeHierarchy(typeDef);
        return new StaticCommandDefinition(
            Name: null,
            Description: null,
            IsDefault: true,
            IsHidden: false,
            Values: values.OrderBy(v => v.Index).ToArray(),
            Options: options.OrderByDescending(o => o.IsRequired).ThenBy(o => o.LongName).ThenBy(o => o.ShortName).ToArray());
    }

    private static (List<StaticOptionDefinition> Options, List<StaticValueDefinition> Values) ReadPropertiesFromTypeHierarchy(TypeDef typeDef)
    {
        var options = new List<StaticOptionDefinition>();
        var values = new List<StaticValueDefinition>();

        foreach (var property in StaticAnalysisHierarchySupport.GetPropertiesFromHierarchy(typeDef))
        {
            var optionAttribute = StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, OptionAttributeName);
            if (optionAttribute is not null)
            {
                options.Add(ReadOptionDefinition(property.Name?.String, property.PropertySig?.RetType, optionAttribute));
                continue;
            }

            var valueAttribute = StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, ValueAttributeName);
            if (valueAttribute is not null)
            {
                values.Add(ReadValueDefinition(property.Name?.String, property.PropertySig?.RetType, valueAttribute));
            }
        }

        foreach (var field in StaticAnalysisHierarchySupport.GetFieldsFromHierarchy(typeDef))
        {
            if (field.IsStatic || field.IsSpecialName || field.Name?.String.Contains("k__BackingField", StringComparison.Ordinal) == true)
            {
                continue;
            }

            var optionAttribute = StaticAnalysisAttributeSupport.FindAttribute(field.CustomAttributes, OptionAttributeName);
            if (optionAttribute is not null)
            {
                options.Add(ReadOptionDefinition(field.Name?.String, field.FieldSig?.Type, optionAttribute));
                continue;
            }

            var valueAttribute = StaticAnalysisAttributeSupport.FindAttribute(field.CustomAttributes, ValueAttributeName);
            if (valueAttribute is not null)
            {
                values.Add(ReadValueDefinition(field.Name?.String, field.FieldSig?.Type, valueAttribute));
            }
        }

        return (options, values);
    }

    private static StaticOptionDefinition ReadOptionDefinition(string? memberName, TypeSig? memberType, CustomAttribute attribute)
    {
        var (longName, shortName) = ParseOptionConstructorArgs(attribute);
        var isRequired = StaticAnalysisAttributeSupport.GetNamedArgumentBool(attribute, "Required");
        var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(attribute, "HelpText");
        var defaultValue = StaticAnalysisAttributeSupport.GetNamedArgumentValueAsString(attribute, "Default");
        var metaValue = StaticAnalysisAttributeSupport.GetNamedArgumentString(attribute, "MetaValue");
        var isHidden = StaticAnalysisAttributeSupport.GetNamedArgumentBool(attribute, "Hidden");
        var clrType = StaticAnalysisTypeSupport.GetClrTypeName(memberType);
        var isBoolLike = StaticAnalysisTypeSupport.IsBoolType(memberType);
        var isSequence = StaticAnalysisTypeSupport.IsSequenceType(memberType);
        var acceptedValues = StaticAnalysisTypeSupport.GetAcceptedValues(memberType);

        return new StaticOptionDefinition(
            LongName: longName ?? CmdParserHeuristicReaderSupport.NormalizeOptionName(memberName),
            ShortName: shortName,
            IsRequired: isRequired,
            IsSequence: isSequence,
            IsBoolLike: isBoolLike,
            ClrType: clrType,
            Description: description,
            DefaultValue: defaultValue,
            MetaValue: metaValue,
            AcceptedValues: acceptedValues,
            PropertyName: memberName);
    }

    private static StaticValueDefinition ReadValueDefinition(string? memberName, TypeSig? memberType, CustomAttribute attribute)
    {
        var index = StaticAnalysisAttributeSupport.GetConstructorArgumentInt(attribute, 0);
        var isRequired = StaticAnalysisAttributeSupport.GetNamedArgumentBool(attribute, "Required");
        var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(attribute, "HelpText");
        var metaName = StaticAnalysisAttributeSupport.GetNamedArgumentString(attribute, "MetaName");
        var defaultValue = StaticAnalysisAttributeSupport.GetNamedArgumentValueAsString(attribute, "Default");
        var clrType = StaticAnalysisTypeSupport.GetClrTypeName(memberType);
        var isSequence = StaticAnalysisTypeSupport.IsSequenceType(memberType);
        var acceptedValues = StaticAnalysisTypeSupport.GetAcceptedValues(memberType);

        return new StaticValueDefinition(
            Index: index,
            Name: metaName ?? memberName?.ToLowerInvariant(),
            IsRequired: isRequired,
            IsSequence: isSequence,
            ClrType: clrType,
            Description: description,
            DefaultValue: defaultValue,
            AcceptedValues: acceptedValues);
    }

    private static (string? LongName, char? ShortName) ParseOptionConstructorArgs(CustomAttribute attribute)
    {
        string? longName = null;
        char? shortName = null;

        foreach (var arg in attribute.ConstructorArguments)
        {
            if (arg.Type.FullName == "System.String" && arg.Value is UTF8String utf8String)
            {
                longName = utf8String.String;
            }
            else if (arg.Type.FullName == "System.Char" && arg.Value is char charValue)
            {
                shortName = charValue;
            }
        }

        return (longName, shortName);
    }

    private static bool HasOptionOrValueProperties(TypeDef typeDef)
    {
        foreach (var property in StaticAnalysisHierarchySupport.GetPropertiesFromHierarchy(typeDef))
        {
            if (StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, OptionAttributeName) is not null
                || StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, ValueAttributeName) is not null)
            {
                return true;
            }
        }

        foreach (var field in StaticAnalysisHierarchySupport.GetFieldsFromHierarchy(typeDef))
        {
            if (StaticAnalysisAttributeSupport.FindAttribute(field.CustomAttributes, OptionAttributeName) is not null
                || StaticAnalysisAttributeSupport.FindAttribute(field.CustomAttributes, ValueAttributeName) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<TypeDef> GetAllTypes(ModuleDefMD module)
    {
        foreach (var typeDef in module.GetTypes())
        {
            yield return typeDef;
        }
    }

}
