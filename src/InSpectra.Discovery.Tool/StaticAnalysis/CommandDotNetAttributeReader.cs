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

                var commandAttr = FindAttribute(typeDef.CustomAttributes, CommandAttributeName);
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
        var className = commandAttr is not null ? GetNamedArgumentString(commandAttr, "Name") : null;
        var classDescription = commandAttr is not null ? GetNamedArgumentString(commandAttr, "Description") : null;

        foreach (var method in typeDef.Methods)
        {
            if (!method.IsPublic || method.IsConstructor || method.IsSpecialName || method.IsStatic)
            {
                continue;
            }

            var methodCommandAttr = FindAttribute(method.CustomAttributes, CommandAttributeName);
            var isDefault = FindAttribute(method.CustomAttributes, DefaultCommandAttributeName) is not null;
            var methodName = methodCommandAttr is not null ? GetNamedArgumentString(methodCommandAttr, "Name") : null;
            var methodDescription = methodCommandAttr is not null ? GetNamedArgumentString(methodCommandAttr, "Description") : null;

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

            if (!commands.TryGetValue(key, out var existing) || Score(definition) > Score(existing))
            {
                commands[key] = definition;
            }
        }

        foreach (var property in typeDef.Properties)
        {
            if (FindAttribute(property.CustomAttributes, SubcommandAttributeName) is null)
            {
                continue;
            }

            var subType = property.PropertySig?.RetType?.ToTypeDefOrRef()?.ResolveTypeDef();
            if (subType is not null)
            {
                var subAttr = FindAttribute(subType.CustomAttributes, CommandAttributeName);
                ReadClassCommands(subType, subAttr, commands);
            }
        }
    }

    private static List<StaticOptionDefinition> ReadPropertyOptions(TypeDef typeDef)
    {
        var options = new List<StaticOptionDefinition>();
        foreach (var property in GetPropertiesFromHierarchy(typeDef))
        {
            var optionAttr = FindAttribute(property.CustomAttributes, OptionAttributeName);
            if (optionAttr is null)
            {
                continue;
            }

            var longName = GetNamedArgumentString(optionAttr, "LongName") ?? property.Name?.String?.ToLowerInvariant();
            var shortNameStr = GetNamedArgumentString(optionAttr, "ShortName");
            var description = GetNamedArgumentString(optionAttr, "Description");
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

            var optionAttr = FindAttribute(param.ParamDef.CustomAttributes, OptionAttributeName);
            if (optionAttr is not null)
            {
                var longName = GetNamedArgumentString(optionAttr, "LongName") ?? param.Name;
                var shortNameStr = GetNamedArgumentString(optionAttr, "ShortName");
                var description = GetNamedArgumentString(optionAttr, "Description");

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

            var operandAttr = FindAttribute(param.ParamDef.CustomAttributes, OperandAttributeName);
            var opName = operandAttr is not null ? GetNamedArgumentString(operandAttr, "Name") : param.Name;
            var opDesc = operandAttr is not null ? GetNamedArgumentString(operandAttr, "Description") : null;

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
            if (FindAttribute(property.CustomAttributes, OptionAttributeName) is not null
                || FindAttribute(property.CustomAttributes, OperandAttributeName) is not null
                || FindAttribute(property.CustomAttributes, SubcommandAttributeName) is not null)
                return true;
        foreach (var method in typeDef.Methods)
            if (FindAttribute(method.CustomAttributes, CommandAttributeName) is not null
                || FindAttribute(method.CustomAttributes, DefaultCommandAttributeName) is not null)
                return true;
        return false;
    }

    private static bool IsCancellationToken(TypeSig? typeSig)
        => string.Equals(typeSig?.FullName, "System.Threading.CancellationToken", StringComparison.Ordinal);

    private static IEnumerable<PropertyDef> GetPropertiesFromHierarchy(TypeDef typeDef)
    {
        var chain = new Stack<TypeDef>();
        for (var current = typeDef; current is not null; current = ResolveBaseType(current))
            chain.Push(current);
        while (chain.Count > 0)
            foreach (var p in chain.Pop().Properties)
                yield return p;
    }

    private static TypeDef? ResolveBaseType(TypeDef typeDef)
    {
        var b = typeDef.BaseType;
        return b is null || string.Equals(b.FullName, "System.Object", StringComparison.Ordinal) ? null : b.ResolveTypeDef();
    }

    private static CustomAttribute? FindAttribute(CustomAttributeCollection attributes, string fullName)
    {
        foreach (var attr in attributes)
            if (string.Equals(attr.AttributeType?.FullName, fullName, StringComparison.Ordinal))
                return attr;
        return null;
    }

    private static string? GetNamedArgumentString(CustomAttribute attr, string name)
    {
        foreach (var a in attr.NamedArguments)
            if (string.Equals(a.Name?.String, name, StringComparison.Ordinal))
                return a.Value is UTF8String u ? u.String : a.Value as string;
        return null;
    }

    private static int Score(StaticCommandDefinition d)
        => d.Values.Count + d.Options.Count + (d.Description is null ? 0 : 1);
}
