using dnlib.DotNet;

internal sealed class CommandDotNetAttributeReader : IStaticAttributeReader
{
    private const string CommandAttributeName = "CommandDotNet.CommandAttribute";
    private const string OptionAttributeName = "CommandDotNet.OptionAttribute";
    private const string OperandAttributeName = "CommandDotNet.OperandAttribute";
    private const string SubcommandAttributeName = "CommandDotNet.SubcommandAttribute";
    private const string DefaultCommandAttributeName = "CommandDotNet.DefaultCommandAttribute";

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

                var commandAttr = StaticAnalysisAttributeSupport.FindAttribute(typeDef.CustomAttributes, CommandAttributeName);
                if (commandAttr is null && !HasDecoratedMembers(typeDef))
                {
                    continue;
                }

                ReadClassCommands(typeDef, commandAttr, commands);
            }
        }

        return commands;
    }

    private static void ReadClassCommands(TypeDef typeDef, CustomAttribute? commandAttr, Dictionary<string, StaticCommandDefinition> commands)
    {
        var classDescription = StaticAnalysisAttributeSupport.GetNamedArgumentString(commandAttr, "Description");

        foreach (var method in typeDef.Methods)
        {
            if (!method.IsPublic || method.IsConstructor || method.IsSpecialName || method.IsStatic)
            {
                continue;
            }

            var methodCommandAttr = StaticAnalysisAttributeSupport.FindAttribute(method.CustomAttributes, CommandAttributeName);
            var isDefault = StaticAnalysisAttributeSupport.FindAttribute(method.CustomAttributes, DefaultCommandAttributeName) is not null;
            var methodName = StaticAnalysisAttributeSupport.GetNamedArgumentString(methodCommandAttr, "Name");
            var methodDescription = StaticAnalysisAttributeSupport.GetNamedArgumentString(methodCommandAttr, "Description");

            var key = methodName ?? (isDefault ? string.Empty : method.Name?.String?.ToLowerInvariant() ?? string.Empty);
            var (options, operands) = ReadMethodParameters(method);
            var propertyOptions = ReadPropertyOptions(typeDef);
            var allOptions = propertyOptions.Concat(options).ToList();

            var definition = new StaticCommandDefinition(
                Name: string.IsNullOrEmpty(key) ? null : key,
                Description: methodDescription ?? classDescription,
                IsDefault: isDefault || string.IsNullOrEmpty(key),
                IsHidden: false,
                Values: operands.OrderBy(v => v.Index).ToArray(),
                Options: allOptions.OrderByDescending(o => o.IsRequired).ThenBy(o => o.LongName).ToArray());

            StaticCommandDefinitionSupport.UpsertBest(commands, key, definition);
        }

        foreach (var property in typeDef.Properties)
        {
            if (StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, SubcommandAttributeName) is null)
            {
                continue;
            }

            var subType = property.PropertySig?.RetType?.ToTypeDefOrRef()?.ResolveTypeDef();
            if (subType is not null)
            {
                var subAttr = StaticAnalysisAttributeSupport.FindAttribute(subType.CustomAttributes, CommandAttributeName);
                ReadClassCommands(subType, subAttr, commands);
            }
        }
    }

    private static List<StaticOptionDefinition> ReadPropertyOptions(TypeDef typeDef)
    {
        var options = new List<StaticOptionDefinition>();
        foreach (var property in StaticAnalysisHierarchySupport.GetPropertiesFromHierarchy(typeDef))
        {
            var optionAttr = StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, OptionAttributeName);
            if (optionAttr is null)
            {
                continue;
            }

            var longName = StaticAnalysisAttributeSupport.GetNamedArgumentString(optionAttr, "LongName") ?? property.Name?.String?.ToLowerInvariant();
            var shortNameStr = StaticAnalysisAttributeSupport.GetNamedArgumentString(optionAttr, "ShortName");
            var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(optionAttr, "Description");
            var propertyType = property.PropertySig?.RetType;

            options.Add(new StaticOptionDefinition(
                LongName: longName,
                ShortName: shortNameStr?.Length == 1 ? shortNameStr[0] : null,
                IsRequired: false,
                IsSequence: StaticAnalysisTypeSupport.IsSequenceType(propertyType),
                IsBoolLike: StaticAnalysisTypeSupport.IsBoolType(propertyType),
                ClrType: StaticAnalysisTypeSupport.GetClrTypeName(propertyType),
                Description: description,
                DefaultValue: null,
                MetaValue: null,
                AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(propertyType),
                PropertyName: property.Name?.String));
        }

        return options;
    }

    private static (List<StaticOptionDefinition> Options, List<StaticValueDefinition> Operands) ReadMethodParameters(MethodDef method)
    {
        var options = new List<StaticOptionDefinition>();
        var operands = new List<StaticValueDefinition>();
        var operandIndex = 0;

        foreach (var param in method.Parameters)
        {
            if (param.IsHiddenThisParameter || param.ParamDef is null)
            {
                continue;
            }

            if (IsCancellationToken(param.Type))
            {
                continue;
            }

            var optionAttr = StaticAnalysisAttributeSupport.FindAttribute(param.ParamDef.CustomAttributes, OptionAttributeName);
            if (optionAttr is not null)
            {
                var longName = StaticAnalysisAttributeSupport.GetNamedArgumentString(optionAttr, "LongName") ?? param.Name;
                var shortNameStr = StaticAnalysisAttributeSupport.GetNamedArgumentString(optionAttr, "ShortName");
                var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(optionAttr, "Description");

                options.Add(new StaticOptionDefinition(
                    LongName: longName,
                    ShortName: shortNameStr?.Length == 1 ? shortNameStr[0] : null,
                    IsRequired: !(param.ParamDef?.HasConstant ?? false),
                    IsSequence: StaticAnalysisTypeSupport.IsSequenceType(param.Type),
                    IsBoolLike: StaticAnalysisTypeSupport.IsBoolType(param.Type),
                    ClrType: StaticAnalysisTypeSupport.GetClrTypeName(param.Type),
                    Description: description,
                    DefaultValue: null,
                    MetaValue: null,
                    AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(param.Type),
                    PropertyName: param.Name));
                continue;
            }

            var operandAttr = StaticAnalysisAttributeSupport.FindAttribute(param.ParamDef.CustomAttributes, OperandAttributeName);
            var opName = StaticAnalysisAttributeSupport.GetNamedArgumentString(operandAttr, "Name") ?? param.Name;
            var opDesc = StaticAnalysisAttributeSupport.GetNamedArgumentString(operandAttr, "Description");

            operands.Add(new StaticValueDefinition(
                Index: operandIndex++,
                Name: opName,
                IsRequired: !(param.ParamDef?.HasConstant ?? false),
                IsSequence: StaticAnalysisTypeSupport.IsSequenceType(param.Type),
                ClrType: StaticAnalysisTypeSupport.GetClrTypeName(param.Type),
                Description: opDesc,
                DefaultValue: null,
                AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(param.Type)));
        }

        return (options, operands);
    }

    private static bool HasDecoratedMembers(TypeDef typeDef)
    {
        foreach (var property in typeDef.Properties)
            if (StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, OptionAttributeName) is not null
                || StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, OperandAttributeName) is not null
                || StaticAnalysisAttributeSupport.FindAttribute(property.CustomAttributes, SubcommandAttributeName) is not null)
                return true;
        foreach (var method in typeDef.Methods)
            if (StaticAnalysisAttributeSupport.FindAttribute(method.CustomAttributes, CommandAttributeName) is not null
                || StaticAnalysisAttributeSupport.FindAttribute(method.CustomAttributes, DefaultCommandAttributeName) is not null)
                return true;
        return false;
    }

    private static bool IsCancellationToken(TypeSig? typeSig)
        => string.Equals(typeSig?.FullName, "System.Threading.CancellationToken", StringComparison.Ordinal);

}
