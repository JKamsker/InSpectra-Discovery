using dnlib.DotNet;

internal sealed class CoconaAttributeReader : IStaticAttributeReader
{
    private static readonly string[] CommandAttributeNames =
    [
        "Cocona.CommandAttribute",
        "Cocona.Lite.CommandAttribute",
    ];

    private static readonly string[] OptionAttributeNames =
    [
        "Cocona.OptionAttribute",
        "Cocona.Lite.OptionAttribute",
    ];

    private static readonly string[] ArgumentAttributeNames =
    [
        "Cocona.ArgumentAttribute",
        "Cocona.Lite.ArgumentAttribute",
    ];

    private static readonly string[] IgnoreAttributeNames =
    [
        "Cocona.IgnoreAttribute",
        "Cocona.Lite.IgnoreAttribute",
    ];

    private static readonly string[] PrimaryCommandAttributeNames =
    [
        "Cocona.PrimaryCommandAttribute",
        "Cocona.Lite.PrimaryCommandAttribute",
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

                ReadCommandMethods(typeDef, commands);
            }
        }

        return commands;
    }

    private static void ReadCommandMethods(TypeDef typeDef, Dictionary<string, StaticCommandDefinition> commands)
    {
        foreach (var method in typeDef.Methods)
        {
            if (!method.IsPublic || method.IsStatic || method.IsConstructor || method.IsSpecialName)
            {
                continue;
            }

            if (StaticAnalysisAttributeSupport.FindAttribute(method.CustomAttributes, IgnoreAttributeNames) is not null)
            {
                continue;
            }

            var commandAttr = StaticAnalysisAttributeSupport.FindAttribute(method.CustomAttributes, CommandAttributeNames);
            var isPrimary = StaticAnalysisAttributeSupport.FindAttribute(method.CustomAttributes, PrimaryCommandAttributeNames) is not null;
            var name = StaticAnalysisAttributeSupport.GetConstructorArgumentString(commandAttr, 0);
            var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(commandAttr, "Description");

            var isDefault = isPrimary || (name is null && !commands.ContainsKey(string.Empty));
            var key = name ?? (isDefault ? string.Empty : method.Name?.String?.ToLowerInvariant() ?? string.Empty);

            var (options, values) = ReadMethodParameters(method);
            var definition = new StaticCommandDefinition(
                Name: string.IsNullOrEmpty(key) ? null : key,
                Description: description,
                IsDefault: isDefault,
                IsHidden: false,
                Values: values,
                Options: options);

            StaticCommandDefinitionSupport.UpsertBest(commands, key, definition);
        }
    }

    private static (IReadOnlyList<StaticOptionDefinition> Options, IReadOnlyList<StaticValueDefinition> Values) ReadMethodParameters(MethodDef method)
    {
        var options = new List<StaticOptionDefinition>();
        var values = new List<StaticValueDefinition>();
        var valueIndex = 0;

        foreach (var param in method.Parameters)
        {
            if (param.IsHiddenThisParameter)
            {
                continue;
            }

            var paramDef = param.ParamDef;
            if (paramDef is null)
            {
                continue;
            }

            if (IsCancellationToken(param.Type))
            {
                continue;
            }

            var optionAttr = StaticAnalysisAttributeSupport.FindAttribute(paramDef.CustomAttributes, OptionAttributeNames);
            if (optionAttr is not null)
            {
                options.Add(ReadOptionFromParameter(param, optionAttr));
                continue;
            }

            var argumentAttr = StaticAnalysisAttributeSupport.FindAttribute(paramDef.CustomAttributes, ArgumentAttributeNames);
            if (argumentAttr is not null)
            {
                values.Add(ReadValueFromParameter(param, argumentAttr, valueIndex));
                valueIndex++;
                continue;
            }

            if (StaticAnalysisTypeSupport.IsBoolType(param.Type))
            {
                options.Add(new StaticOptionDefinition(
                    LongName: ConvertToKebabCase(param.Name),
                    ShortName: null,
                    IsRequired: false,
                    IsSequence: false,
                    IsBoolLike: true,
                    ClrType: StaticAnalysisTypeSupport.GetClrTypeName(param.Type),
                    Description: null,
                    DefaultValue: null,
                    MetaValue: null,
                    AcceptedValues: [],
                    PropertyName: param.Name));
            }
            else
            {
                options.Add(new StaticOptionDefinition(
                    LongName: ConvertToKebabCase(param.Name),
                    ShortName: null,
                    IsRequired: !(param.ParamDef?.HasConstant ?? false),
                    IsSequence: StaticAnalysisTypeSupport.IsSequenceType(param.Type),
                    IsBoolLike: false,
                    ClrType: StaticAnalysisTypeSupport.GetClrTypeName(param.Type),
                    Description: null,
                    DefaultValue: null,
                    MetaValue: null,
                    AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(param.Type),
                    PropertyName: param.Name));
            }
        }

        return (options, values);
    }

    private static StaticOptionDefinition ReadOptionFromParameter(Parameter param, CustomAttribute attr)
    {
        var longName = StaticAnalysisAttributeSupport.GetConstructorArgumentString(attr, 0) ?? ConvertToKebabCase(param.Name);
        var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(attr, "Description");
        var shortNames = StaticAnalysisAttributeSupport.GetNamedArgumentStrings(attr, "ShortNames");

        return new StaticOptionDefinition(
            LongName: longName,
            ShortName: shortNames.Length > 0 && shortNames[0].Length == 1 ? shortNames[0][0] : null,
            IsRequired: !(param.ParamDef?.HasConstant ?? false),
            IsSequence: StaticAnalysisTypeSupport.IsSequenceType(param.Type),
            IsBoolLike: StaticAnalysisTypeSupport.IsBoolType(param.Type),
            ClrType: StaticAnalysisTypeSupport.GetClrTypeName(param.Type),
            Description: description,
            DefaultValue: null,
            MetaValue: null,
            AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(param.Type),
            PropertyName: param.Name);
    }

    private static StaticValueDefinition ReadValueFromParameter(Parameter param, CustomAttribute attr, int index)
    {
        var name = StaticAnalysisAttributeSupport.GetNamedArgumentString(attr, "Name") ?? param.Name;
        var description = StaticAnalysisAttributeSupport.GetNamedArgumentString(attr, "Description");
        var order = StaticAnalysisAttributeSupport.GetNamedArgumentInt(attr, "Order", index);

        return new StaticValueDefinition(
            Index: order,
            Name: name,
            IsRequired: !(param.ParamDef?.HasConstant ?? false),
            IsSequence: StaticAnalysisTypeSupport.IsSequenceType(param.Type),
            ClrType: StaticAnalysisTypeSupport.GetClrTypeName(param.Type),
            Description: description,
            DefaultValue: null,
            AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(param.Type));
    }

    private static string ConvertToKebabCase(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "value";
        var result = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) result.Append('-');
            result.Append(char.ToLowerInvariant(name[i]));
        }

        return result.ToString();
    }

    private static bool IsCancellationToken(TypeSig? typeSig)
        => string.Equals(typeSig?.FullName, "System.Threading.CancellationToken", StringComparison.Ordinal);

}
