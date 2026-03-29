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

            if (FindAttribute(method.CustomAttributes, IgnoreAttributeNames) is not null)
            {
                continue;
            }

            var commandAttr = FindAttribute(method.CustomAttributes, CommandAttributeNames);
            var isPrimary = FindAttribute(method.CustomAttributes, PrimaryCommandAttributeNames) is not null;
            var name = commandAttr is not null ? GetConstructorArgumentString(commandAttr, 0) : null;
            var description = commandAttr is not null ? GetNamedArgumentString(commandAttr, "Description") : null;

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

            if (!commands.TryGetValue(key, out var existing) || Score(definition) > Score(existing))
            {
                commands[key] = definition;
            }
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

            var optionAttr = FindAttribute(paramDef.CustomAttributes, OptionAttributeNames);
            if (optionAttr is not null)
            {
                options.Add(ReadOptionFromParameter(param, optionAttr));
                continue;
            }

            var argumentAttr = FindAttribute(paramDef.CustomAttributes, ArgumentAttributeNames);
            if (argumentAttr is not null)
            {
                values.Add(ReadValueFromParameter(param, argumentAttr, valueIndex));
                valueIndex++;
                continue;
            }

            if (IsBoolType(param.Type))
            {
                options.Add(new StaticOptionDefinition(
                    LongName: ConvertToKebabCase(param.Name),
                    ShortName: null,
                    IsRequired: false,
                    IsSequence: false,
                    IsBoolLike: true,
                    ClrType: GetClrTypeName(param.Type),
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
                    IsSequence: IsSequenceType(param.Type),
                    IsBoolLike: false,
                    ClrType: GetClrTypeName(param.Type),
                    Description: null,
                    DefaultValue: null,
                    MetaValue: null,
                    AcceptedValues: GetAcceptedValues(param.Type),
                    PropertyName: param.Name));
            }
        }

        return (options, values);
    }

    private static StaticOptionDefinition ReadOptionFromParameter(Parameter param, CustomAttribute attr)
    {
        var longName = GetConstructorArgumentString(attr, 0) ?? ConvertToKebabCase(param.Name);
        var description = GetNamedArgumentString(attr, "Description");
        var shortNames = GetNamedArgumentStrings(attr, "ShortNames");

        return new StaticOptionDefinition(
            LongName: longName,
            ShortName: shortNames.Length > 0 && shortNames[0].Length == 1 ? shortNames[0][0] : null,
            IsRequired: !(param.ParamDef?.HasConstant ?? false),
            IsSequence: IsSequenceType(param.Type),
            IsBoolLike: IsBoolType(param.Type),
            ClrType: GetClrTypeName(param.Type),
            Description: description,
            DefaultValue: null,
            MetaValue: null,
            AcceptedValues: GetAcceptedValues(param.Type),
            PropertyName: param.Name);
    }

    private static StaticValueDefinition ReadValueFromParameter(Parameter param, CustomAttribute attr, int index)
    {
        var name = GetNamedArgumentString(attr, "Name") ?? param.Name;
        var description = GetNamedArgumentString(attr, "Description");
        var order = GetNamedArgumentInt(attr, "Order", index);

        return new StaticValueDefinition(
            Index: order,
            Name: name,
            IsRequired: !(param.ParamDef?.HasConstant ?? false),
            IsSequence: IsSequenceType(param.Type),
            ClrType: GetClrTypeName(param.Type),
            Description: description,
            DefaultValue: null,
            AcceptedValues: GetAcceptedValues(param.Type));
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

    private static CustomAttribute? FindAttribute(CustomAttributeCollection attributes, string[] names)
    {
        foreach (var attr in attributes)
        {
            foreach (var name in names)
            {
                if (string.Equals(attr.AttributeType?.FullName, name, StringComparison.Ordinal))
                    return attr;
            }
        }

        return null;
    }

    private static string? GetConstructorArgumentString(CustomAttribute attr, int index)
        => index < attr.ConstructorArguments.Count
            ? attr.ConstructorArguments[index].Value is UTF8String u ? u.String : attr.ConstructorArguments[index].Value as string
            : null;

    private static string? GetNamedArgumentString(CustomAttribute attr, string name)
    {
        foreach (var a in attr.NamedArguments)
            if (string.Equals(a.Name?.String, name, StringComparison.Ordinal))
                return a.Value is UTF8String u ? u.String : a.Value as string;
        return null;
    }

    private static int GetNamedArgumentInt(CustomAttribute attr, string name, int fallback)
    {
        foreach (var a in attr.NamedArguments)
            if (string.Equals(a.Name?.String, name, StringComparison.Ordinal) && a.Value is int v)
                return v;
        return fallback;
    }

    private static string[] GetNamedArgumentStrings(CustomAttribute attr, string name)
    {
        foreach (var a in attr.NamedArguments)
        {
            if (!string.Equals(a.Name?.String, name, StringComparison.Ordinal)) continue;
            if (a.Value is IList<CAArgument> list)
                return list.Select(x => x.Value is UTF8String u ? u.String : x.Value?.ToString() ?? string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }

        return [];
    }

    private static IReadOnlyList<string> GetAcceptedValues(TypeSig? typeSig)
    {
        var resolved = UnwrapNullable(typeSig)?.ToTypeDefOrRef()?.ResolveTypeDef();
        return resolved is { IsEnum: true }
            ? resolved.Fields.Where(f => !f.IsSpecialName && f.IsStatic).Select(f => f.Name.String).ToArray()
            : [];
    }

    private static string? GetClrTypeName(TypeSig? typeSig)
    {
        if (typeSig is null) return null;
        if (typeSig is SZArraySig a) return $"{GetClrTypeName(a.Next)}[]";
        if (typeSig is GenericInstSig g)
        {
            var n = g.GenericType?.FullName?.Split('`')[0];
            if (string.Equals(n, "System.Nullable", StringComparison.Ordinal) && g.GenericArguments.Count == 1)
                return $"System.Nullable<{GetClrTypeName(g.GenericArguments[0])}>";
            return $"{n}<{string.Join(", ", g.GenericArguments.Select(GetClrTypeName))}>";
        }

        return typeSig.FullName;
    }

    private static bool IsBoolType(TypeSig? typeSig)
        => string.Equals(UnwrapNullable(typeSig)?.FullName, "System.Boolean", StringComparison.Ordinal);

    private static bool IsSequenceType(TypeSig? typeSig)
    {
        if (typeSig is SZArraySig) return true;
        if (string.Equals(typeSig?.FullName, "System.String", StringComparison.Ordinal)) return false;
        if (typeSig is GenericInstSig g)
        {
            var n = g.GenericType?.FullName?.Split('`')[0];
            if (string.Equals(n, "System.Nullable", StringComparison.Ordinal)) return false;
            return n is "System.Collections.Generic.IEnumerable" or "System.Collections.Generic.IList"
                or "System.Collections.Generic.ICollection" or "System.Collections.Generic.List"
                or "System.Collections.Generic.IReadOnlyList" or "System.Collections.Generic.IReadOnlyCollection";
        }

        return false;
    }

    private static TypeSig? UnwrapNullable(TypeSig? typeSig)
        => typeSig is GenericInstSig g
            && string.Equals(g.GenericType?.FullName?.Split('`')[0], "System.Nullable", StringComparison.Ordinal)
            && g.GenericArguments.Count == 1
            ? g.GenericArguments[0] : typeSig;

    private static int Score(StaticCommandDefinition d)
        => d.Values.Count + d.Options.Count + (d.Description is null ? 0 : 1);
}
