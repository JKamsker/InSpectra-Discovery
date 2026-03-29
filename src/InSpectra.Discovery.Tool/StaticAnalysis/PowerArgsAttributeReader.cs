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
                if (!commands.TryGetValue(key, out var existing) || Score(definition) > Score(existing))
                {
                    commands[key] = definition;
                }

                ReadActionTypes(typeDef, commands);
            }
        }

        return commands;
    }

    private static StaticCommandDefinition ReadTypeDefinition(TypeDef typeDef)
    {
        var description = GetAttributeString(typeDef.CustomAttributes, ArgDescriptionAttribute);
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
            var actionTypeAttr = FindAttribute(property.CustomAttributes, ArgActionTypeAttribute);
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

                var methodDescription = GetAttributeString(method.CustomAttributes, ArgDescriptionAttribute);
                var (methodOptions, methodValues) = ReadMethodParameters(method);
                var actionName = method.Name?.String?.ToLowerInvariant() ?? string.Empty;

                var actionDef = new StaticCommandDefinition(
                    Name: actionName,
                    Description: methodDescription,
                    IsDefault: false,
                    IsHidden: false,
                    Values: methodValues,
                    Options: methodOptions);

                if (!commands.TryGetValue(actionName, out var existing) || Score(actionDef) > Score(existing))
                {
                    commands[actionName] = actionDef;
                }
            }
        }
    }

    private static (List<StaticOptionDefinition> Options, List<StaticValueDefinition> Values) ReadProperties(TypeDef typeDef)
    {
        var options = new List<StaticOptionDefinition>();
        var values = new List<StaticValueDefinition>();

        foreach (var property in GetPropertiesFromHierarchy(typeDef))
        {
            if (FindAttribute(property.CustomAttributes, ArgActionTypeAttribute) is not null)
            {
                continue;
            }

            var positionAttr = FindAttribute(property.CustomAttributes, ArgPositionAttribute);
            if (positionAttr is not null)
            {
                var index = positionAttr.ConstructorArguments.Count > 0 && positionAttr.ConstructorArguments[0].Value is int i ? i : 0;
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
                IsSequence: IsSequenceType(param.Type),
                IsBoolLike: IsBoolType(param.Type),
                ClrType: GetClrTypeName(param.Type),
                Description: param.ParamDef is not null ? GetAttributeString(param.ParamDef.CustomAttributes, ArgDescriptionAttribute) : null,
                DefaultValue: null,
                MetaValue: null,
                AcceptedValues: GetAcceptedValues(param.Type),
                PropertyName: param.Name));
        }

        return (options, values);
    }

    private static StaticOptionDefinition ReadOptionDefinition(PropertyDef property)
    {
        var description = GetAttributeString(property.CustomAttributes, ArgDescriptionAttribute);
        var isRequired = FindAttribute(property.CustomAttributes, ArgRequiredAttribute) is not null;
        var shortcut = GetShortcut(property.CustomAttributes);
        var defaultValue = GetDefaultValue(property.CustomAttributes);
        var propertyType = property.PropertySig?.RetType;

        return new StaticOptionDefinition(
            LongName: property.Name?.String,
            ShortName: shortcut?.Length == 1 ? shortcut[0] : null,
            IsRequired: isRequired,
            IsSequence: IsSequenceType(propertyType),
            IsBoolLike: IsBoolType(propertyType),
            ClrType: GetClrTypeName(propertyType),
            Description: description,
            DefaultValue: defaultValue,
            MetaValue: null,
            AcceptedValues: GetAcceptedValues(propertyType),
            PropertyName: property.Name?.String);
    }

    private static StaticValueDefinition ReadValueDefinition(PropertyDef property, int index)
    {
        var description = GetAttributeString(property.CustomAttributes, ArgDescriptionAttribute);
        var isRequired = FindAttribute(property.CustomAttributes, ArgRequiredAttribute) is not null;
        var propertyType = property.PropertySig?.RetType;

        return new StaticValueDefinition(
            Index: index,
            Name: property.Name?.String?.ToLowerInvariant(),
            IsRequired: isRequired,
            IsSequence: IsSequenceType(propertyType),
            ClrType: GetClrTypeName(propertyType),
            Description: description,
            DefaultValue: null,
            AcceptedValues: GetAcceptedValues(propertyType));
    }

    private static string? GetShortcut(CustomAttributeCollection attributes)
    {
        var attr = FindAttribute(attributes, ArgShortcutAttribute);
        return attr?.ConstructorArguments.Count > 0
            ? attr.ConstructorArguments[0].Value is UTF8String u ? u.String : attr.ConstructorArguments[0].Value as string
            : null;
    }

    private static string? GetDefaultValue(CustomAttributeCollection attributes)
    {
        var attr = FindAttribute(attributes, ArgDefaultValueAttribute);
        return attr?.ConstructorArguments.Count > 0 ? attr.ConstructorArguments[0].Value?.ToString() : null;
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

    private static string? GetAttributeString(CustomAttributeCollection attributes, string attributeName)
    {
        var attr = FindAttribute(attributes, attributeName);
        return attr?.ConstructorArguments.Count > 0
            ? attr.ConstructorArguments[0].Value is UTF8String u ? u.String : attr.ConstructorArguments[0].Value as string
            : null;
    }

    private static IEnumerable<PropertyDef> GetPropertiesFromHierarchy(TypeDef typeDef)
    {
        var chain = new Stack<TypeDef>();
        for (var current = typeDef; current is not null; current = ResolveBaseType(current))
            chain.Push(current);
        while (chain.Count > 0)
            foreach (var property in chain.Pop().Properties)
                yield return property;
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
                or "System.Collections.Generic.ICollection" or "System.Collections.Generic.List";
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
