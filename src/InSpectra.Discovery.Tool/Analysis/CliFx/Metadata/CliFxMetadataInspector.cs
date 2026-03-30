namespace InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;

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

            if (!CliFxMetadataReflectionSupport.ReferencesCliFx(assembly))
            {
                continue;
            }

            foreach (var type in CliFxMetadataReflectionSupport.GetLoadableTypes(assembly))
            {
                if (!type.IsClass || type.IsAbstract)
                {
                    continue;
                }

                var commandAttribute = CliFxMetadataReflectionSupport.FindAttribute(type.CustomAttributes, CommandAttributeNames);
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

        foreach (var property in CliFxMetadataReflectionSupport.GetPublicInstanceProperties(type))
        {
            var optionAttribute = CliFxMetadataReflectionSupport.FindAttribute(property.CustomAttributes, OptionAttributeNames);
            if (optionAttribute is not null)
            {
                options.Add(CreateOptionDefinition(property, optionAttribute));
                continue;
            }

            var parameterAttribute = CliFxMetadataReflectionSupport.FindAttribute(property.CustomAttributes, ParameterAttributeNames);
            if (parameterAttribute is not null)
            {
                parameters.Add(CreateParameterDefinition(property, parameterAttribute));
            }
        }

        return new CliFxCommandDefinition(
            Name: commandAttribute.ConstructorArguments.FirstOrDefault(argument => argument.ArgumentType.FullName == typeof(string).FullName).Value as string,
            Description: CliFxMetadataAttributeSupport.GetNamedArgument<string>(commandAttribute, "Description"),
            Parameters: parameters.OrderBy(parameter => parameter.Order).ToArray(),
            Options: options.OrderByDescending(option => option.IsRequired).ThenBy(option => option.Name).ThenBy(option => option.ShortName).ToArray());
    }

    private static CliFxParameterDefinition CreateParameterDefinition(PropertyInfo property, CustomAttributeData attribute)
    {
        var order = attribute.ConstructorArguments.FirstOrDefault(argument => argument.ArgumentType.FullName == typeof(int).FullName).Value as int? ?? 0;
        return new CliFxParameterDefinition(
            Order: order,
            Name: CliFxMetadataAttributeSupport.GetNamedArgument<string>(attribute, "Name") ?? property.Name.ToLowerInvariant(),
            IsRequired: CliFxMetadataTypeSupport.IsRequired(property),
            IsSequence: CliFxMetadataTypeSupport.IsSequence(property, attribute),
            ClrType: CliFxMetadataTypeSupport.GetClrTypeName(property.PropertyType),
            Description: CliFxMetadataAttributeSupport.GetNamedArgument<string>(attribute, "Description"),
            AcceptedValues: CliFxMetadataTypeSupport.GetAcceptedValues(property.PropertyType));
    }

    private static CliFxOptionDefinition CreateOptionDefinition(PropertyInfo property, CustomAttributeData attribute)
    {
        var name = CliFxOptionNameSupport.NormalizeLongName(
            attribute.ConstructorArguments.FirstOrDefault(argument => argument.ArgumentType.FullName == typeof(string).FullName).Value as string);
        var shortName = attribute.ConstructorArguments.FirstOrDefault(argument => argument.ArgumentType.FullName == typeof(char).FullName).Value as char?;
        var propertyType = property.PropertyType;
        var clrType = CliFxMetadataTypeSupport.GetClrTypeName(propertyType);
        var acceptedValues = CliFxMetadataTypeSupport.GetAcceptedValues(propertyType);
        var nullableUnderlyingType = Nullable.GetUnderlyingType(propertyType);
        var boolType = nullableUnderlyingType ?? propertyType;

        return new CliFxOptionDefinition(
            Name: name,
            ShortName: shortName,
            IsRequired: CliFxMetadataTypeSupport.IsRequired(property),
            IsSequence: CliFxMetadataTypeSupport.IsSequence(property, attribute),
            IsBoolLike: string.Equals(boolType.FullName, typeof(bool).FullName, StringComparison.Ordinal),
            ClrType: clrType,
            Description: CliFxMetadataAttributeSupport.GetNamedArgument<string>(attribute, "Description"),
            EnvironmentVariable: CliFxMetadataAttributeSupport.GetNamedArgument<string>(attribute, "EnvironmentVariable"),
            AcceptedValues: acceptedValues,
            ValueName: property.Name);
    }

    private static int Score(CliFxCommandDefinition definition)
        => definition.Parameters.Count + definition.Options.Count + (definition.Description is null ? 0 : 1);
}

