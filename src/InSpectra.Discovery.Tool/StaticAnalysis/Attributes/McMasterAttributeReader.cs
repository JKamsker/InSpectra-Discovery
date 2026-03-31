namespace InSpectra.Discovery.Tool.StaticAnalysis.Attributes;

using InSpectra.Discovery.Tool.StaticAnalysis.Models;

using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

using dnlib.DotNet;

internal sealed class McMasterAttributeReader : IStaticAttributeReader
{
    private const string CommandAttributeName = "McMaster.Extensions.CommandLineUtils.CommandAttribute";
    private const string OptionAttributeName = "McMaster.Extensions.CommandLineUtils.OptionAttribute";
    private const string ArgumentAttributeName = "McMaster.Extensions.CommandLineUtils.ArgumentAttribute";
    private const string SubcommandAttributeName = "McMaster.Extensions.CommandLineUtils.SubcommandAttribute";
    private const string HelpOptionAttributeName = "McMaster.Extensions.CommandLineUtils.HelpOptionAttribute";
    private const string VersionOptionAttributeName = "McMaster.Extensions.CommandLineUtils.VersionOptionAttribute";

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

                var commandAttribute = StaticAnalysisAttributeSupport.FindAttribute(typeDef.CustomAttributes, CommandAttributeName);
                var hasOptionsOrArguments = HasDecoratedProperties(typeDef);
                if (commandAttribute is null && !hasOptionsOrArguments)
                {
                    continue;
                }

                var definition = ReadCommandDefinition(typeDef, commandAttribute);
                var key = definition.Name ?? string.Empty;
                StaticCommandDefinitionSupport.UpsertBest(commands, key, definition);

                ReadSubcommands(typeDef, commands);
            }
        }

        return commands;
    }

    private static StaticCommandDefinition ReadCommandDefinition(TypeDef typeDef, CustomAttribute? commandAttribute)
    {
        var name = StaticAnalysisAttributeSupport.GetConstructorArgumentString(commandAttribute, 0);
        var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(commandAttribute, "Description");
        var (options, arguments) = ReadProperties(typeDef);

        return new StaticCommandDefinition(
            Name: name,
            Description: description,
            IsDefault: string.IsNullOrEmpty(name),
            IsHidden: false,
            Values: arguments.OrderBy(a => a.Index).ToArray(),
            Options: options.OrderByDescending(o => o.IsRequired).ThenBy(o => o.LongName).ThenBy(o => o.ShortName).ToArray());
    }

    private static void ReadSubcommands(TypeDef typeDef, Dictionary<string, StaticCommandDefinition> commands)
    {
        foreach (var attr in typeDef.CustomAttributes)
        {
            if (!string.Equals(attr.AttributeType?.FullName, SubcommandAttributeName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Value is TypeDefOrRefSig typeSig)
                {
                    var resolved = typeSig.TypeDefOrRef?.ResolveTypeDef();
                    if (resolved is not null)
                    {
                        var subCommandAttr = StaticAnalysisAttributeSupport.FindAttribute(resolved.CustomAttributes, CommandAttributeName);
                        var subDef = ReadCommandDefinition(resolved, subCommandAttr);
                        var subKey = subDef.Name ?? string.Empty;
                        StaticCommandDefinitionSupport.UpsertBest(commands, subKey, subDef);
                    }
                }
            }
        }
    }

    private static (List<StaticOptionDefinition> Options, List<StaticValueDefinition> Arguments) ReadProperties(TypeDef typeDef)
    {
        var options = new List<StaticOptionDefinition>();
        var arguments = new List<StaticValueDefinition>();
        var argumentIndex = 0;

        foreach (var property in StaticAnalysisHierarchySupport.GetPropertiesFromHierarchy(typeDef))
        {
            if (StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, HelpOptionAttributeName) is not null
                || StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, VersionOptionAttributeName) is not null)
            {
                continue;
            }

            var optionAttr = StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, OptionAttributeName);
            if (optionAttr is not null)
            {
                options.Add(ReadOptionDefinition(property, optionAttr));
                continue;
            }

            var argumentAttr = StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, ArgumentAttributeName);
            if (argumentAttr is not null)
            {
                arguments.Add(ReadArgumentDefinition(property, argumentAttr, argumentIndex));
                argumentIndex++;
            }
        }

        return (options, arguments);
    }

    private static StaticOptionDefinition ReadOptionDefinition(PropertyDef property, CustomAttribute attribute)
    {
        var template = StaticAnalysisAttributeSupport.GetNamedArgumentString(attribute, "Template")
            ?? StaticAnalysisAttributeSupport.GetConstructorArgumentString(attribute, 0);
        var (longName, shortName) = ParseTemplate(template, property.Name?.String);
        var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(attribute, "Description");
        var propertyType = property.PropertySig?.RetType;

        return new StaticOptionDefinition(
            LongName: longName,
            ShortName: shortName,
            IsRequired: false,
            IsSequence: StaticAnalysisTypeSupport.IsSequenceType(propertyType),
            IsBoolLike: StaticAnalysisTypeSupport.IsBoolType(propertyType),
            ClrType: StaticAnalysisTypeSupport.GetClrTypeName(propertyType),
            Description: description,
            DefaultValue: null,
            MetaValue: null,
            AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(propertyType),
            PropertyName: property.Name?.String);
    }

    private static StaticValueDefinition ReadArgumentDefinition(PropertyDef property, CustomAttribute attribute, int fallbackIndex)
    {
        var name = StaticAnalysisAttributeSupport.GetNamedArgumentString(attribute, "Name") ?? property.Name?.String?.ToLowerInvariant();
        var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(attribute, "Description");
        var propertyType = property.PropertySig?.RetType;

        return new StaticValueDefinition(
            Index: fallbackIndex,
            Name: name,
            IsRequired: false,
            IsSequence: StaticAnalysisTypeSupport.IsSequenceType(propertyType),
            ClrType: StaticAnalysisTypeSupport.GetClrTypeName(propertyType),
            Description: description,
            DefaultValue: null,
            AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(propertyType));
    }

    private static (string? LongName, char? ShortName) ParseTemplate(string? template, string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return (propertyName?.ToLowerInvariant(), null);
        }

        string? longName = null;
        char? shortName = null;
        foreach (var part in template.Split('|', ' '))
        {
            var trimmed = part.Trim().TrimEnd(':');
            if (trimmed.StartsWith("--", StringComparison.Ordinal) && trimmed.Length > 2)
            {
                longName = trimmed[2..];
            }
            else if (trimmed.StartsWith("-", StringComparison.Ordinal) && trimmed.Length == 2 && char.IsLetterOrDigit(trimmed[1]))
            {
                shortName = trimmed[1];
            }
            else if (!trimmed.StartsWith("-", StringComparison.Ordinal) && trimmed.Length > 0)
            {
                longName ??= trimmed;
            }
        }

        return (longName ?? propertyName?.ToLowerInvariant(), shortName);
    }

    private static bool HasDecoratedProperties(TypeDef typeDef)
    {
        foreach (var property in typeDef.Properties)
        {
            if (StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, OptionAttributeName) is not null
                || StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, ArgumentAttributeName) is not null)
            {
                return true;
            }
        }

        return false;
    }

}

