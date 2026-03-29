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

                var commandAttribute = FindAttribute(typeDef.CustomAttributes, CommandAttributeName);
                var hasOptionsOrArguments = HasDecoratedProperties(typeDef);
                if (commandAttribute is null && !hasOptionsOrArguments)
                {
                    continue;
                }

                var definition = ReadCommandDefinition(typeDef, commandAttribute, scannedModule.Module);
                var key = definition.Name ?? string.Empty;
                if (!commands.TryGetValue(key, out var existing) || Score(definition) > Score(existing))
                {
                    commands[key] = definition;
                }

                ReadSubcommands(typeDef, scannedModule.Module, commands);
            }
        }

        return commands;
    }

    private static StaticCommandDefinition ReadCommandDefinition(TypeDef typeDef, CustomAttribute? commandAttribute, ModuleDefMD module)
    {
        var name = commandAttribute is not null ? GetConstructorArgumentString(commandAttribute, 0) : null;
        var description = commandAttribute is not null ? GetNamedArgumentString(commandAttribute, "Description") : null;
        var (options, arguments) = ReadProperties(typeDef, module);

        return new StaticCommandDefinition(
            Name: name,
            Description: description,
            IsDefault: string.IsNullOrEmpty(name),
            IsHidden: false,
            Values: arguments.OrderBy(a => a.Index).ToArray(),
            Options: options.OrderByDescending(o => o.IsRequired).ThenBy(o => o.LongName).ThenBy(o => o.ShortName).ToArray());
    }

    private static void ReadSubcommands(TypeDef typeDef, ModuleDefMD module, Dictionary<string, StaticCommandDefinition> commands)
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
                        var subCommandAttr = FindAttribute(resolved.CustomAttributes, CommandAttributeName);
                        var subDef = ReadCommandDefinition(resolved, subCommandAttr, module);
                        var subKey = subDef.Name ?? string.Empty;
                        if (!commands.ContainsKey(subKey) || Score(subDef) > Score(commands[subKey]))
                        {
                            commands[subKey] = subDef;
                        }
                    }
                }
            }
        }
    }

    private static (List<StaticOptionDefinition> Options, List<StaticValueDefinition> Arguments) ReadProperties(TypeDef typeDef, ModuleDefMD module)
    {
        var options = new List<StaticOptionDefinition>();
        var arguments = new List<StaticValueDefinition>();
        var argumentIndex = 0;

        foreach (var property in GetPropertiesFromHierarchy(typeDef))
        {
            if (FindAttribute(property.CustomAttributes, HelpOptionAttributeName) is not null
                || FindAttribute(property.CustomAttributes, VersionOptionAttributeName) is not null)
            {
                continue;
            }

            var optionAttr = FindAttribute(property.CustomAttributes, OptionAttributeName);
            if (optionAttr is not null)
            {
                options.Add(ReadOptionDefinition(property, optionAttr, module));
                continue;
            }

            var argumentAttr = FindAttribute(property.CustomAttributes, ArgumentAttributeName);
            if (argumentAttr is not null)
            {
                arguments.Add(ReadArgumentDefinition(property, argumentAttr, module, argumentIndex));
                argumentIndex++;
            }
        }

        return (options, arguments);
    }

    private static StaticOptionDefinition ReadOptionDefinition(PropertyDef property, CustomAttribute attribute, ModuleDefMD module)
    {
        var template = GetNamedArgumentString(attribute, "Template")
            ?? GetConstructorArgumentString(attribute, 0);
        var (longName, shortName) = ParseTemplate(template, property.Name?.String);
        var description = GetNamedArgumentString(attribute, "Description");
        var propertyType = property.PropertySig?.RetType;

        return new StaticOptionDefinition(
            LongName: longName,
            ShortName: shortName,
            IsRequired: false,
            IsSequence: IsSequenceType(propertyType),
            IsBoolLike: IsBoolType(propertyType),
            ClrType: GetClrTypeName(propertyType),
            Description: description,
            DefaultValue: null,
            MetaValue: null,
            AcceptedValues: GetAcceptedValues(propertyType),
            PropertyName: property.Name?.String);
    }

    private static StaticValueDefinition ReadArgumentDefinition(PropertyDef property, CustomAttribute attribute, ModuleDefMD module, int fallbackIndex)
    {
        var name = GetNamedArgumentString(attribute, "Name") ?? property.Name?.String?.ToLowerInvariant();
        var description = GetNamedArgumentString(attribute, "Description");
        var propertyType = property.PropertySig?.RetType;

        return new StaticValueDefinition(
            Index: fallbackIndex,
            Name: name,
            IsRequired: false,
            IsSequence: IsSequenceType(propertyType),
            ClrType: GetClrTypeName(propertyType),
            Description: description,
            DefaultValue: null,
            AcceptedValues: GetAcceptedValues(propertyType));
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
            if (FindAttribute(property.CustomAttributes, OptionAttributeName) is not null
                || FindAttribute(property.CustomAttributes, ArgumentAttributeName) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<PropertyDef> GetPropertiesFromHierarchy(TypeDef typeDef)
    {
        var chain = new Stack<TypeDef>();
        for (var current = typeDef; current is not null; current = ResolveBaseType(current))
        {
            chain.Push(current);
        }

        while (chain.Count > 0)
        {
            foreach (var property in chain.Pop().Properties)
            {
                yield return property;
            }
        }
    }

    private static TypeDef? ResolveBaseType(TypeDef typeDef)
    {
        var baseTypeRef = typeDef.BaseType;
        if (baseTypeRef is null || string.Equals(baseTypeRef.FullName, "System.Object", StringComparison.Ordinal))
        {
            return null;
        }

        return baseTypeRef.ResolveTypeDef();
    }

    private static CustomAttribute? FindAttribute(CustomAttributeCollection attributes, string fullName)
    {
        foreach (var attr in attributes)
        {
            if (string.Equals(attr.AttributeType?.FullName, fullName, StringComparison.Ordinal))
            {
                return attr;
            }
        }

        return null;
    }

    private static string? GetConstructorArgumentString(CustomAttribute attribute, int index)
        => index < attribute.ConstructorArguments.Count
            ? attribute.ConstructorArguments[index].Value is UTF8String u ? u.String : attribute.ConstructorArguments[index].Value as string
            : null;

    private static string? GetNamedArgumentString(CustomAttribute attribute, string name)
    {
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (string.Equals(namedArg.Name?.String, name, StringComparison.Ordinal))
            {
                return namedArg.Value is UTF8String u ? u.String : namedArg.Value as string;
            }
        }

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
        if (typeSig is SZArraySig arraySig) return $"{GetClrTypeName(arraySig.Next)}[]";
        if (typeSig is GenericInstSig g)
        {
            var name = g.GenericType?.FullName?.Split('`')[0];
            if (string.Equals(name, "System.Nullable", StringComparison.Ordinal) && g.GenericArguments.Count == 1)
                return $"System.Nullable<{GetClrTypeName(g.GenericArguments[0])}>";
            return $"{name}<{string.Join(", ", g.GenericArguments.Select(GetClrTypeName))}>";
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
            var name = g.GenericType?.FullName?.Split('`')[0];
            if (string.Equals(name, "System.Nullable", StringComparison.Ordinal)) return false;
            return name is "System.Collections.Generic.IEnumerable" or "System.Collections.Generic.IList"
                or "System.Collections.Generic.ICollection" or "System.Collections.Generic.List"
                or "System.Collections.Generic.IReadOnlyList" or "System.Collections.Generic.IReadOnlyCollection";
        }

        return false;
    }

    private static TypeSig? UnwrapNullable(TypeSig? typeSig)
        => typeSig is GenericInstSig g
            && string.Equals(g.GenericType?.FullName?.Split('`')[0], "System.Nullable", StringComparison.Ordinal)
            && g.GenericArguments.Count == 1
            ? g.GenericArguments[0]
            : typeSig;

    private static int Score(StaticCommandDefinition d)
        => d.Values.Count + d.Options.Count + (d.Description is null ? 0 : 1);
}
