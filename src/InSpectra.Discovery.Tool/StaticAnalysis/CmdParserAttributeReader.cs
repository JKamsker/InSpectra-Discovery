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
        var verblessTypes = new List<(TypeDef Type, ModuleDefMD Module)>();

        foreach (var scannedModule in modules)
        {
            foreach (var typeDef in GetAllTypes(scannedModule.Module))
            {
                if (!typeDef.IsClass || typeDef.IsAbstract || typeDef.IsInterface)
                {
                    continue;
                }

                var verbAttribute = FindAttribute(typeDef.CustomAttributes, VerbAttributeName);
                if (verbAttribute is not null)
                {
                    hasAnyVerb = true;
                    var definition = ReadVerbDefinition(typeDef, verbAttribute, scannedModule.Module);
                    var key = definition.Name ?? string.Empty;
                    if (!commands.TryGetValue(key, out var existing) || Score(definition) > Score(existing))
                    {
                        commands[key] = definition;
                    }
                }
                else if (HasOptionOrValueProperties(typeDef))
                {
                    verblessTypes.Add((typeDef, scannedModule.Module));
                }
            }
        }

        if (!hasAnyVerb)
        {
            foreach (var (typeDef, module) in verblessTypes)
            {
                var definition = ReadVerblessDefinition(typeDef, module);
                var key = string.Empty;
                if (!commands.TryGetValue(key, out var existing) || Score(definition) > Score(existing))
                {
                    commands[key] = definition;
                }
            }
        }

        return commands;
    }

    private static StaticCommandDefinition ReadVerbDefinition(TypeDef typeDef, CustomAttribute verbAttribute, ModuleDefMD module)
    {
        var name = GetConstructorArgumentString(verbAttribute, 0);
        var description = GetNamedArgumentString(verbAttribute, "HelpText");
        var isDefault = GetNamedArgumentBool(verbAttribute, "IsDefault");
        var isHidden = GetNamedArgumentBool(verbAttribute, "Hidden");
        var (options, values) = ReadPropertiesFromTypeHierarchy(typeDef, module);

        return new StaticCommandDefinition(
            Name: name,
            Description: description,
            IsDefault: isDefault,
            IsHidden: isHidden,
            Values: values.OrderBy(v => v.Index).ToArray(),
            Options: options.OrderByDescending(o => o.IsRequired).ThenBy(o => o.LongName).ThenBy(o => o.ShortName).ToArray());
    }

    private static StaticCommandDefinition ReadVerblessDefinition(TypeDef typeDef, ModuleDefMD module)
    {
        var (options, values) = ReadPropertiesFromTypeHierarchy(typeDef, module);
        return new StaticCommandDefinition(
            Name: null,
            Description: null,
            IsDefault: true,
            IsHidden: false,
            Values: values.OrderBy(v => v.Index).ToArray(),
            Options: options.OrderByDescending(o => o.IsRequired).ThenBy(o => o.LongName).ThenBy(o => o.ShortName).ToArray());
    }

    private static (List<StaticOptionDefinition> Options, List<StaticValueDefinition> Values) ReadPropertiesFromTypeHierarchy(TypeDef typeDef, ModuleDefMD module)
    {
        var options = new List<StaticOptionDefinition>();
        var values = new List<StaticValueDefinition>();

        foreach (var property in GetPropertiesFromTypeHierarchy(typeDef))
        {
            var optionAttribute = FindAttribute(property.CustomAttributes, OptionAttributeName);
            if (optionAttribute is not null)
            {
                options.Add(ReadOptionDefinition(property, optionAttribute, module));
                continue;
            }

            var valueAttribute = FindAttribute(property.CustomAttributes, ValueAttributeName);
            if (valueAttribute is not null)
            {
                values.Add(ReadValueDefinition(property, valueAttribute, module));
            }
        }

        return (options, values);
    }

    private static StaticOptionDefinition ReadOptionDefinition(PropertyDef property, CustomAttribute attribute, ModuleDefMD module)
    {
        var (longName, shortName) = ParseOptionConstructorArgs(attribute);
        var isRequired = GetNamedArgumentBool(attribute, "Required");
        var description = GetNamedArgumentString(attribute, "HelpText");
        var defaultValue = GetNamedArgumentValueAsString(attribute, "Default");
        var metaValue = GetNamedArgumentString(attribute, "MetaValue");
        var isHidden = GetNamedArgumentBool(attribute, "Hidden");
        var propertyType = property.PropertySig?.RetType;
        var clrType = GetClrTypeName(propertyType);
        var isBoolLike = IsBoolType(propertyType);
        var isSequence = IsSequenceType(propertyType);
        var acceptedValues = GetAcceptedValues(propertyType, module);

        return new StaticOptionDefinition(
            LongName: longName,
            ShortName: shortName,
            IsRequired: isRequired,
            IsSequence: isSequence,
            IsBoolLike: isBoolLike,
            ClrType: clrType,
            Description: description,
            DefaultValue: defaultValue,
            MetaValue: metaValue,
            AcceptedValues: acceptedValues,
            PropertyName: property.Name?.String);
    }

    private static StaticValueDefinition ReadValueDefinition(PropertyDef property, CustomAttribute attribute, ModuleDefMD module)
    {
        var index = GetConstructorArgumentInt(attribute, 0);
        var isRequired = GetNamedArgumentBool(attribute, "Required");
        var description = GetNamedArgumentString(attribute, "HelpText");
        var metaName = GetNamedArgumentString(attribute, "MetaName");
        var defaultValue = GetNamedArgumentValueAsString(attribute, "Default");
        var propertyType = property.PropertySig?.RetType;
        var clrType = GetClrTypeName(propertyType);
        var isSequence = IsSequenceType(propertyType);
        var acceptedValues = GetAcceptedValues(propertyType, module);

        return new StaticValueDefinition(
            Index: index,
            Name: metaName ?? property.Name?.String?.ToLowerInvariant(),
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
        foreach (var property in typeDef.Properties)
        {
            if (FindAttribute(property.CustomAttributes, OptionAttributeName) is not null
                || FindAttribute(property.CustomAttributes, ValueAttributeName) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<PropertyDef> GetPropertiesFromTypeHierarchy(TypeDef typeDef)
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
        if (baseTypeRef is null
            || string.Equals(baseTypeRef.FullName, "System.Object", StringComparison.Ordinal))
        {
            return null;
        }

        return baseTypeRef.ResolveTypeDef();
    }

    private static IEnumerable<TypeDef> GetAllTypes(ModuleDefMD module)
    {
        foreach (var typeDef in module.GetTypes())
        {
            yield return typeDef;
        }
    }

    private static CustomAttribute? FindAttribute(CustomAttributeCollection attributes, string fullName)
    {
        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute.AttributeType?.FullName, fullName, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        return null;
    }

    private static string? GetConstructorArgumentString(CustomAttribute attribute, int index)
    {
        if (index >= attribute.ConstructorArguments.Count)
        {
            return null;
        }

        return attribute.ConstructorArguments[index].Value is UTF8String utf8String
            ? utf8String.String
            : attribute.ConstructorArguments[index].Value as string;
    }

    private static int GetConstructorArgumentInt(CustomAttribute attribute, int index)
    {
        if (index >= attribute.ConstructorArguments.Count)
        {
            return 0;
        }

        return attribute.ConstructorArguments[index].Value is int intValue ? intValue : 0;
    }

    private static string? GetNamedArgumentString(CustomAttribute attribute, string name)
    {
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (string.Equals(namedArg.Name?.String, name, StringComparison.Ordinal))
            {
                return namedArg.Value is UTF8String utf8String
                    ? utf8String.String
                    : namedArg.Value as string;
            }
        }

        return null;
    }

    private static bool GetNamedArgumentBool(CustomAttribute attribute, string name)
    {
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (string.Equals(namedArg.Name?.String, name, StringComparison.Ordinal))
            {
                return namedArg.Value is bool boolValue && boolValue;
            }
        }

        return false;
    }

    private static string? GetNamedArgumentValueAsString(CustomAttribute attribute, string name)
    {
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (string.Equals(namedArg.Name?.String, name, StringComparison.Ordinal))
            {
                return namedArg.Value?.ToString();
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetAcceptedValues(TypeSig? typeSig, ModuleDefMD module)
    {
        var resolvedType = UnwrapNullable(typeSig)?.ToTypeDefOrRef()?.ResolveTypeDef();
        if (resolvedType is null || !resolvedType.IsEnum)
        {
            return [];
        }

        return resolvedType.Fields
            .Where(field => !field.IsSpecialName && field.IsStatic)
            .Select(field => field.Name.String)
            .ToArray();
    }

    private static string? GetClrTypeName(TypeSig? typeSig)
    {
        if (typeSig is null)
        {
            return null;
        }

        if (typeSig is SZArraySig arraySig)
        {
            return $"{GetClrTypeName(arraySig.Next)}[]";
        }

        if (typeSig is GenericInstSig genericInstSig)
        {
            var genericName = genericInstSig.GenericType?.FullName?.Split('`')[0];
            if (string.Equals(genericName, "System.Nullable", StringComparison.Ordinal)
                && genericInstSig.GenericArguments.Count == 1)
            {
                return $"System.Nullable<{GetClrTypeName(genericInstSig.GenericArguments[0])}>";
            }

            var args = string.Join(", ", genericInstSig.GenericArguments.Select(GetClrTypeName));
            return $"{genericName}<{args}>";
        }

        return typeSig.FullName;
    }

    private static bool IsBoolType(TypeSig? typeSig)
    {
        var unwrapped = UnwrapNullable(typeSig);
        return string.Equals(unwrapped?.FullName, "System.Boolean", StringComparison.Ordinal);
    }

    private static bool IsSequenceType(TypeSig? typeSig)
    {
        if (typeSig is SZArraySig)
        {
            return true;
        }

        if (string.Equals(typeSig?.FullName, "System.String", StringComparison.Ordinal))
        {
            return false;
        }

        if (typeSig is GenericInstSig genericInstSig)
        {
            var genericName = genericInstSig.GenericType?.FullName?.Split('`')[0];
            if (string.Equals(genericName, "System.Nullable", StringComparison.Ordinal))
            {
                return false;
            }

            return genericName is "System.Collections.Generic.IEnumerable"
                or "System.Collections.Generic.IList"
                or "System.Collections.Generic.ICollection"
                or "System.Collections.Generic.IReadOnlyList"
                or "System.Collections.Generic.IReadOnlyCollection"
                or "System.Collections.Generic.List"
                or "System.Collections.Generic.HashSet"
                or "System.Collections.Generic.ISet";
        }

        return false;
    }

    private static TypeSig? UnwrapNullable(TypeSig? typeSig)
    {
        if (typeSig is GenericInstSig genericInstSig
            && string.Equals(genericInstSig.GenericType?.FullName?.Split('`')[0], "System.Nullable", StringComparison.Ordinal)
            && genericInstSig.GenericArguments.Count == 1)
        {
            return genericInstSig.GenericArguments[0];
        }

        return typeSig;
    }

    private static int Score(StaticCommandDefinition definition)
        => definition.Values.Count + definition.Options.Count + (definition.Description is null ? 0 : 1);
}
