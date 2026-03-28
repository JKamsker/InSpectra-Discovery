using System.Reflection;

internal sealed class CliFxMetadataInspector
{
    private static readonly string[] CommandAttributeNames =
    [
        "CliFx.Binding.CommandAttribute",
        "CliFx.Attributes.CommandAttribute",
    ];

    private static readonly string[] OptionAttributeNames =
    [
        "CliFx.Binding.CommandOptionAttribute",
        "CliFx.Attributes.CommandOptionAttribute",
    ];

    private static readonly string[] ParameterAttributeNames =
    [
        "CliFx.Binding.CommandParameterAttribute",
        "CliFx.Attributes.CommandParameterAttribute",
    ];

    public IReadOnlyDictionary<string, CliFxCommandDefinition> Inspect(string installDirectory)
    {
        var assemblyPaths = Directory.EnumerateFiles(installDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (assemblyPaths.Length == 0)
        {
            return new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        var runtimeAssemblyPaths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var resolver = new PathAssemblyResolver(assemblyPaths.Concat(runtimeAssemblyPaths).Distinct(StringComparer.OrdinalIgnoreCase));
        using var metadataLoadContext = new MetadataLoadContext(resolver);

        var commands = new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyPath in assemblyPaths)
        {
            Assembly assembly;
            try
            {
                assembly = metadataLoadContext.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
            }
            catch (BadImageFormatException)
            {
                continue;
            }
            catch (FileLoadException)
            {
                continue;
            }

            if (!ReferencesCliFx(assembly))
            {
                continue;
            }

            foreach (var type in GetLoadableTypes(assembly))
            {
                if (!type.IsClass || type.IsAbstract)
                {
                    continue;
                }

                var commandAttribute = FindAttribute(type.CustomAttributes, CommandAttributeNames);
                if (commandAttribute is null)
                {
                    continue;
                }

                var commandDefinition = CreateCommandDefinition(type, commandAttribute);
                var commandKey = commandDefinition.Name ?? string.Empty;
                if (!commands.TryGetValue(commandKey, out var existing)
                    || Score(commandDefinition) > Score(existing))
                {
                    commands[commandKey] = commandDefinition;
                }
            }
        }

        return commands;
    }

    private static CliFxCommandDefinition CreateCommandDefinition(Type type, CustomAttributeData commandAttribute)
    {
        var parameters = new List<CliFxParameterDefinition>();
        var options = new List<CliFxOptionDefinition>();

        foreach (var property in GetPublicInstanceProperties(type))
        {
            var optionAttribute = FindAttribute(property.CustomAttributes, OptionAttributeNames);
            if (optionAttribute is not null)
            {
                options.Add(CreateOptionDefinition(property, optionAttribute));
                continue;
            }

            var parameterAttribute = FindAttribute(property.CustomAttributes, ParameterAttributeNames);
            if (parameterAttribute is not null)
            {
                parameters.Add(CreateParameterDefinition(property, parameterAttribute));
            }
        }

        return new CliFxCommandDefinition(
            Name: commandAttribute.ConstructorArguments.FirstOrDefault(argument => argument.ArgumentType.FullName == typeof(string).FullName).Value as string,
            Description: GetNamedArgument<string>(commandAttribute, "Description"),
            Parameters: parameters.OrderBy(parameter => parameter.Order).ToArray(),
            Options: options.OrderByDescending(option => option.IsRequired).ThenBy(option => option.Name).ThenBy(option => option.ShortName).ToArray());
    }

    private static CliFxParameterDefinition CreateParameterDefinition(PropertyInfo property, CustomAttributeData attribute)
    {
        var order = attribute.ConstructorArguments.FirstOrDefault(argument => argument.ArgumentType.FullName == typeof(int).FullName).Value as int? ?? 0;
        return new CliFxParameterDefinition(
            Order: order,
            Name: GetNamedArgument<string>(attribute, "Name") ?? property.Name.ToLowerInvariant(),
            IsRequired: IsRequired(property),
            IsSequence: IsSequence(property, attribute),
            ClrType: GetClrTypeName(property.PropertyType),
            Description: GetNamedArgument<string>(attribute, "Description"),
            AcceptedValues: GetAcceptedValues(property.PropertyType));
    }

    private static CliFxOptionDefinition CreateOptionDefinition(PropertyInfo property, CustomAttributeData attribute)
    {
        var name = CliFxOptionNameSupport.NormalizeLongName(
            attribute.ConstructorArguments.FirstOrDefault(argument => argument.ArgumentType.FullName == typeof(string).FullName).Value as string);
        var shortName = attribute.ConstructorArguments.FirstOrDefault(argument => argument.ArgumentType.FullName == typeof(char).FullName).Value as char?;
        var propertyType = property.PropertyType;
        var clrType = GetClrTypeName(propertyType);
        var acceptedValues = GetAcceptedValues(propertyType);
        var nullableUnderlyingType = Nullable.GetUnderlyingType(propertyType);
        var boolType = nullableUnderlyingType ?? propertyType;

        return new CliFxOptionDefinition(
            Name: name,
            ShortName: shortName,
            IsRequired: IsRequired(property),
            IsSequence: IsSequence(property, attribute),
            IsBoolLike: string.Equals(boolType.FullName, typeof(bool).FullName, StringComparison.Ordinal),
            ClrType: clrType,
            Description: GetNamedArgument<string>(attribute, "Description"),
            EnvironmentVariable: GetNamedArgument<string>(attribute, "EnvironmentVariable"),
            AcceptedValues: acceptedValues,
            ValueName: property.Name);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }

    private static IEnumerable<PropertyInfo> GetPublicInstanceProperties(Type type)
    {
        var chain = new Stack<Type>();
        for (var current = type; current is not null && !string.Equals(current.FullName, typeof(object).FullName, StringComparison.Ordinal); current = current.BaseType)
        {
            chain.Push(current);
        }

        while (chain.Count > 0)
        {
            foreach (var property in chain.Pop().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                yield return property;
            }
        }
    }

    private static bool ReferencesCliFx(Assembly assembly)
        => string.Equals(assembly.GetName().Name, "CliFx", StringComparison.OrdinalIgnoreCase)
            || assembly.GetReferencedAssemblies().Any(reference => string.Equals(reference.Name, "CliFx", StringComparison.OrdinalIgnoreCase));

    private static CustomAttributeData? FindAttribute(IEnumerable<CustomAttributeData> attributes, IEnumerable<string> fullNames)
        => attributes.FirstOrDefault(attribute => fullNames.Any(fullName =>
            string.Equals(attribute.AttributeType.FullName, fullName, StringComparison.Ordinal)));

    private static T? GetNamedArgument<T>(CustomAttributeData attribute, string name)
    {
        var value = attribute.NamedArguments.FirstOrDefault(argument => string.Equals(argument.MemberName, name, StringComparison.Ordinal)).TypedValue.Value;
        return value is T typedValue ? typedValue : default;
    }

    private static bool IsRequired(PropertyInfo property)
        => property.CustomAttributes.Any(attribute => string.Equals(attribute.AttributeType.FullName, "System.Runtime.CompilerServices.RequiredMemberAttribute", StringComparison.Ordinal));

    private static bool IsSequence(PropertyInfo property, CustomAttributeData attribute)
    {
        var converterType = GetNamedArgument<Type>(attribute, "Converter");
        if (converterType is not null)
        {
            return converterType
                .GetInterfaces()
                .Concat(GetBaseTypes(converterType))
                .Any(type => string.Equals(type.FullName?.Split('`')[0], "CliFx.Activation.SequenceInputConverter", StringComparison.Ordinal));
        }

        return IsSequenceType(property.PropertyType);
    }

    private static IEnumerable<Type> GetBaseTypes(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    private static bool IsSequenceType(Type type)
    {
        if (type.IsArray)
        {
            return true;
        }

        if (string.Equals(type.FullName, typeof(string).FullName, StringComparison.Ordinal))
        {
            return false;
        }

        return type.GetInterfaces().Any(interfaceType =>
            string.Equals(interfaceType.FullName?.Split('`')[0], "System.Collections.Generic.IEnumerable", StringComparison.Ordinal));
    }

    private static string? GetClrTypeName(Type type)
    {
        if (type.IsArray)
        {
            return $"{GetClrTypeName(type.GetElementType()!)}[]";
        }

        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            return $"System.Nullable<{GetClrTypeName(nullableType)}>";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var genericName = type.GetGenericTypeDefinition().FullName?.Split('`')[0] ?? type.Name;
        var genericArguments = string.Join(", ", type.GetGenericArguments().Select(GetClrTypeName));
        return $"{genericName}<{genericArguments}>";
    }

    private static IReadOnlyList<string> GetAcceptedValues(Type type)
    {
        var enumType = Nullable.GetUnderlyingType(type) ?? type;
        return enumType.IsEnum ? Enum.GetNames(enumType) : [];
    }

    private static int Score(CliFxCommandDefinition definition)
        => definition.Parameters.Count + definition.Options.Count + (definition.Description is null ? 0 : 1);
}
