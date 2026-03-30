namespace InSpectra.Discovery.Tool.StaticAnalysis;

using dnlib.DotNet;

internal sealed class PowerArgsAttributeReader : IStaticAttributeReader
{
    private const string ArgRequiredAttribute = "PowerArgs.ArgRequired";
    private const string ArgShortcutAttribute = "PowerArgs.ArgShortcut";
    private const string ArgDescriptionAttribute = "PowerArgs.ArgDescription";
    private const string ArgPositionAttribute = "PowerArgs.ArgPosition";
    private const string ArgDefaultValueAttribute = "PowerArgs.ArgDefaultValue";
    private const string ArgActionTypeAttribute = "PowerArgs.ArgActionType";

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

                if (!HasPowerArgsProperties(typeDef))
                {
                    continue;
                }

                var definition = ReadTypeDefinition(typeDef);
                var key = definition.Name ?? string.Empty;
                StaticCommandDefinitionSupport.UpsertBest(commands, key, definition);

                ReadActionTypes(typeDef, commands);
            }
        }

        return commands;
    }

    private static StaticCommandDefinition ReadTypeDefinition(TypeDef typeDef)
    {
        var description = StaticAnalysisAttributeSupport.GetAttributeString(typeDef.CustomAttributes, ArgDescriptionAttribute);
        var (options, values) = ReadProperties(typeDef);

        return new StaticCommandDefinition(
            Name: null,
            Description: description,
            IsDefault: true,
            IsHidden: false,
            Values: values.OrderBy(v => v.Index).ToArray(),
            Options: options.OrderByDescending(o => o.IsRequired).ThenBy(o => o.LongName).ToArray());
    }

    private static void ReadActionTypes(TypeDef typeDef, Dictionary<string, StaticCommandDefinition> commands)
    {
        foreach (var property in typeDef.Properties)
        {
            var actionTypeAttr = StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, ArgActionTypeAttribute);
            if (actionTypeAttr is null)
            {
                continue;
            }

            var actionTypeSig = actionTypeAttr.ConstructorArguments.FirstOrDefault().Value as TypeDefOrRefSig;
            var actionTypeDef = actionTypeSig?.TypeDefOrRef?.ResolveTypeDef();
            if (actionTypeDef is null)
            {
                continue;
            }

            foreach (var method in actionTypeDef.Methods)
            {
                if (!method.IsPublic || method.IsConstructor || method.IsSpecialName || method.IsStatic)
                {
                    continue;
                }

                var methodDescription = StaticAnalysisAttributeSupport.GetAttributeString(method.CustomAttributes, ArgDescriptionAttribute);
                var (methodOptions, methodValues) = ReadMethodParameters(method);
                var actionName = method.Name?.String?.ToLowerInvariant() ?? string.Empty;

                var actionDef = new StaticCommandDefinition(
                    Name: actionName,
                    Description: methodDescription,
                    IsDefault: false,
                    IsHidden: false,
                    Values: methodValues,
                    Options: methodOptions);

                StaticCommandDefinitionSupport.UpsertBest(commands, actionName, actionDef);
            }
        }
    }

    private static (List<StaticOptionDefinition> Options, List<StaticValueDefinition> Values) ReadProperties(TypeDef typeDef)
    {
        var options = new List<StaticOptionDefinition>();
        var values = new List<StaticValueDefinition>();

        foreach (var property in StaticAnalysisHierarchySupport.GetPropertiesFromHierarchy(typeDef))
        {
            if (StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, ArgActionTypeAttribute) is not null)
            {
                continue;
            }

            var positionAttr = StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, ArgPositionAttribute);
            if (positionAttr is not null)
            {
                var index = StaticAnalysisAttributeSupport.GetConstructorArgumentInt(positionAttr, 0);
                values.Add(ReadValueDefinition(property, index));
                continue;
            }

            options.Add(ReadOptionDefinition(property));
        }

        return (options, values);
    }

    private static (IReadOnlyList<StaticOptionDefinition> Options, IReadOnlyList<StaticValueDefinition> Values) ReadMethodParameters(MethodDef method)
    {
        var options = new List<StaticOptionDefinition>();
        var values = new List<StaticValueDefinition>();

        foreach (var param in method.Parameters)
        {
            if (param.IsHiddenThisParameter || param.ParamDef is null)
            {
                continue;
            }

            options.Add(new StaticOptionDefinition(
                LongName: param.Name,
                ShortName: null,
                IsRequired: !(param.ParamDef?.HasConstant ?? false),
                IsSequence: StaticAnalysisTypeSupport.IsSequenceType(param.Type),
                IsBoolLike: StaticAnalysisTypeSupport.IsBoolType(param.Type),
                ClrType: StaticAnalysisTypeSupport.GetClrTypeName(param.Type),
                Description: param.ParamDef is not null ? StaticAnalysisAttributeSupport.GetAttributeString(param.ParamDef.CustomAttributes, ArgDescriptionAttribute) : null,
                DefaultValue: null,
                MetaValue: null,
                AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(param.Type),
                PropertyName: param.Name));
        }

        return (options, values);
    }

    private static StaticOptionDefinition ReadOptionDefinition(PropertyDef property)
    {
        var description = StaticAnalysisAttributeSupport.GetAttributeString(property.CustomAttributes, ArgDescriptionAttribute);
        var isRequired = StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, ArgRequiredAttribute) is not null;
        var shortcut = GetShortcut(property.CustomAttributes);
        var defaultValue = GetDefaultValue(property.CustomAttributes);
        var propertyType = property.PropertySig?.RetType;

        return new StaticOptionDefinition(
            LongName: property.Name?.String,
            ShortName: shortcut?.Length == 1 ? shortcut[0] : null,
            IsRequired: isRequired,
            IsSequence: StaticAnalysisTypeSupport.IsSequenceType(propertyType),
            IsBoolLike: StaticAnalysisTypeSupport.IsBoolType(propertyType),
            ClrType: StaticAnalysisTypeSupport.GetClrTypeName(propertyType),
            Description: description,
            DefaultValue: defaultValue,
            MetaValue: null,
            AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(propertyType),
            PropertyName: property.Name?.String);
    }

    private static StaticValueDefinition ReadValueDefinition(PropertyDef property, int index)
    {
        var description = StaticAnalysisAttributeSupport.GetAttributeString(property.CustomAttributes, ArgDescriptionAttribute);
        var isRequired = StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, ArgRequiredAttribute) is not null;
        var propertyType = property.PropertySig?.RetType;

        return new StaticValueDefinition(
            Index: index,
            Name: property.Name?.String?.ToLowerInvariant(),
            IsRequired: isRequired,
            IsSequence: StaticAnalysisTypeSupport.IsSequenceType(propertyType),
            ClrType: StaticAnalysisTypeSupport.GetClrTypeName(propertyType),
            Description: description,
            DefaultValue: null,
            AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(propertyType));
    }

    private static string? GetShortcut(CustomAttributeCollection attributes)
    {
        var attribute = StaticAnalysisAttributeSupport.FindAttribute(attributes, ArgShortcutAttribute);
        return StaticAnalysisAttributeSupport.GetConstructorArgumentString(attribute, 0);
    }

    private static string? GetDefaultValue(CustomAttributeCollection attributes)
    {
        var attribute = StaticAnalysisAttributeSupport.FindAttribute(attributes, ArgDefaultValueAttribute);
        return StaticAnalysisAttributeSupport.GetConstructorArgumentValueAsString(attribute, 0);
    }

    private static bool HasPowerArgsProperties(TypeDef typeDef)
    {
        foreach (var property in typeDef.Properties)
        {
            foreach (var attr in property.CustomAttributes)
            {
                var name = attr.AttributeType?.FullName;
                if (name is not null && name.StartsWith("PowerArgs.", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

}

