using System.Reflection;

internal static class CommandLineParserTreeWalker
{
    private const string VerbAttributeName = "CommandLine.VerbAttribute";
    private const string OptionAttributeName = "CommandLine.OptionAttribute";
    private const string ValueAttributeName = "CommandLine.ValueAttribute";

    public static bool TryWalk(object parseResult, out CapturedCommand? root)
    {
        root = null;

        var typeInfo = ReflectionValueReader.GetMemberValue(parseResult, "TypeInfo");
        if (typeInfo is null)
        {
            return false;
        }

        var choiceTypes = ReflectionValueReader.GetEnumerable<Type>(typeInfo, "Choices")
            .Where(IsUsableCommandType)
            .Distinct()
            .ToArray();
        if (choiceTypes.Length > 0)
        {
            root = new CapturedCommand();
            foreach (var choiceType in choiceTypes)
            {
                var command = BuildCommand(choiceType, isRoot: false);
                if (!string.IsNullOrWhiteSpace(command.Name))
                {
                    root.Subcommands.Add(command);
                }
            }

            return root.Subcommands.Count > 0;
        }

        var currentType = ResolveCurrentType(typeInfo);
        if (!IsUsableCommandType(currentType))
        {
            return false;
        }

        root = BuildCommand(currentType!, isRoot: true);
        return root.Options.Count > 0
            || root.Arguments.Count > 0
            || root.Subcommands.Count > 0
            || !string.IsNullOrWhiteSpace(root.Description);
    }

    private static CapturedCommand BuildCommand(Type type, bool isRoot)
    {
        var verbAttribute = FindCustomAttribute(type, VerbAttributeName);
        var command = new CapturedCommand
        {
            Name = isRoot ? null : ResolveCommandName(type, verbAttribute),
            Description = GetNamedArgumentString(verbAttribute, "HelpText"),
            IsHidden = GetNamedArgumentBool(verbAttribute, "Hidden"),
        };

        var attributedOptions = ReadAttributedOptions(type);
        var attributedArguments = ReadAttributedArguments(type);
        if (attributedOptions.Count == 0 && attributedArguments.Count == 0)
        {
            attributedOptions = ReadHeuristicOptions(type);
        }

        command.Options.AddRange(attributedOptions);
        command.Arguments.AddRange(attributedArguments);
        EnsureBuiltInCommandOptions(command);
        return command;
    }

    private static List<CapturedOption> ReadAttributedOptions(Type type)
    {
        var options = new List<CapturedOption>();
        foreach (var member in EnumerateHierarchyMembers(type))
        {
            var optionAttribute = FindCustomAttribute(member, OptionAttributeName);
            if (optionAttribute is null)
            {
                continue;
            }

            options.Add(BuildAttributedOption(member, optionAttribute));
        }

        return DeduplicateOptions(options);
    }

    private static List<CapturedArgument> ReadAttributedArguments(Type type)
    {
        var values = new List<(int Index, CapturedArgument Argument)>();
        foreach (var member in EnumerateHierarchyMembers(type))
        {
            var valueAttribute = FindCustomAttribute(member, ValueAttributeName);
            if (valueAttribute is null)
            {
                continue;
            }

            values.Add((GetConstructorArgumentInt(valueAttribute, 0), BuildAttributedArgument(member, valueAttribute)));
        }

        return values
            .OrderBy(static value => value.Index)
            .Select(static value => value.Argument)
            .ToList();
    }

    private static List<CapturedOption> ReadHeuristicOptions(Type type)
    {
        var options = new List<CapturedOption>();
        foreach (var member in EnumerateHierarchyMembers(type))
        {
            if (!LooksLikeHeuristicOptionMember(member, out var memberType))
            {
                continue;
            }

            var isSequence = IsSequenceType(memberType);
            var isBoolLike = IsBoolType(memberType);
            options.Add(new CapturedOption
            {
                Name = $"--{ConvertToKebabCase(TrimKnownSuffix(member.Name))}",
                Description = null,
                IsRequired = false,
                IsHidden = false,
                MinArity = isBoolLike ? 0 : 0,
                MaxArity = isBoolLike ? 0 : (isSequence ? int.MaxValue : 1),
                ValueType = isBoolLike ? "Void" : FormatTypeName(memberType),
                ArgumentName = isBoolLike ? null : ConvertToArgumentName(member.Name),
            });
        }

        return DeduplicateOptions(options);
    }

    private static CapturedOption BuildAttributedOption(MemberInfo member, CustomAttributeData attribute)
    {
        var memberType = GetMemberType(member);
        var (longName, shortName) = ParseOptionNames(attribute, member.Name);
        var isSequence = IsSequenceType(memberType);
        var isBoolLike = IsBoolType(memberType);
        var captured = new CapturedOption
        {
            Name = longName is not null ? $"--{longName}" : $"-{shortName}",
            Description = GetNamedArgumentString(attribute, "HelpText"),
            IsRequired = GetNamedArgumentBool(attribute, "Required"),
            IsHidden = GetNamedArgumentBool(attribute, "Hidden"),
            MinArity = isBoolLike ? 0 : (GetNamedArgumentBool(attribute, "Required") ? 1 : 0),
            MaxArity = isBoolLike ? 0 : (isSequence ? int.MaxValue : 1),
            ValueType = isBoolLike ? "Void" : FormatTypeName(memberType),
            ArgumentName = GetNamedArgumentString(attribute, "MetaValue") ?? (isBoolLike ? null : ConvertToArgumentName(member.Name)),
            DefaultValue = GetNamedArgumentString(attribute, "Default"),
            HasDefaultValue = !string.IsNullOrWhiteSpace(GetNamedArgumentString(attribute, "Default")),
            AllowedValues = ReadAllowedValues(memberType),
        };

        if (longName is not null && shortName is not null)
        {
            captured.Aliases.Add($"-{shortName}");
        }
        else if (longName is null && shortName is not null)
        {
            captured.Aliases.Add($"--{ConvertToKebabCase(TrimKnownSuffix(member.Name))}");
        }

        return captured;
    }

    private static CapturedArgument BuildAttributedArgument(MemberInfo member, CustomAttributeData attribute)
    {
        var memberType = GetMemberType(member);
        var isSequence = IsSequenceType(memberType);
        var isRequired = GetNamedArgumentBool(attribute, "Required");
        return new CapturedArgument
        {
            Name = GetNamedArgumentString(attribute, "MetaName") ?? member.Name,
            Description = GetNamedArgumentString(attribute, "HelpText"),
            IsHidden = false,
            MinArity = isRequired ? 1 : 0,
            MaxArity = isSequence ? int.MaxValue : 1,
            ValueType = FormatTypeName(memberType),
            HasDefaultValue = !string.IsNullOrWhiteSpace(GetNamedArgumentString(attribute, "Default")),
            DefaultValue = GetNamedArgumentString(attribute, "Default"),
            AllowedValues = ReadAllowedValues(memberType),
        };
    }

    private static IEnumerable<MemberInfo> EnumerateHierarchyMembers(Type type)
    {
        var chain = new Stack<Type>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            chain.Push(current);
        }

        while (chain.Count > 0)
        {
            var current = chain.Pop();
            foreach (var property in current.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.GetIndexParameters().Length == 0)
                {
                    yield return property;
                }
            }

            foreach (var field in current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!field.IsStatic)
                {
                    yield return field;
                }
            }
        }
    }

    private static void EnsureBuiltInCommandOptions(CapturedCommand command)
    {
        if (!command.Options.Any(static option => string.Equals(option.Name, "--help", StringComparison.Ordinal)))
        {
            command.Options.Add(new CapturedOption
            {
                Name = "--help",
                Description = "Display this help screen.",
                Aliases = ["-h"],
                MinArity = 0,
                MaxArity = 0,
                ValueType = "Void",
            });
        }

        if (!command.Options.Any(static option => string.Equals(option.Name, "--version", StringComparison.Ordinal)))
        {
            command.Options.Add(new CapturedOption
            {
                Name = "--version",
                Description = "Display version information.",
                MinArity = 0,
                MaxArity = 0,
                ValueType = "Void",
            });
        }
    }

    private static Type? ResolveCurrentType(object typeInfo)
    {
        var current = ReflectionValueReader.GetMemberValue(typeInfo, "Current");
        if (current is Type currentType)
        {
            return currentType;
        }

        if (current is null || string.Equals(current.GetType().FullName, "CommandLine.NullInstance", StringComparison.Ordinal))
        {
            return null;
        }

        return current.GetType();
    }

    private static bool IsUsableCommandType(Type? type)
        => type is not null
            && type != typeof(object)
            && !string.Equals(type.FullName, "CommandLine.NullInstance", StringComparison.Ordinal);

    private static string? ResolveCommandName(Type type, CustomAttributeData? verbAttribute)
        => GetConstructorArgumentString(verbAttribute, 0)
            ?? ConvertToKebabCase(type.Name.Replace("Options", string.Empty, StringComparison.OrdinalIgnoreCase));

    private static CustomAttributeData? FindCustomAttribute(MemberInfo member, string fullName)
        => member.GetCustomAttributesData()
            .FirstOrDefault(attribute => string.Equals(attribute.AttributeType.FullName, fullName, StringComparison.Ordinal));

    private static string? GetConstructorArgumentString(CustomAttributeData? attribute, int index)
    {
        if (attribute is null || attribute.ConstructorArguments.Count <= index)
        {
            return null;
        }

        return attribute.ConstructorArguments[index].Value?.ToString();
    }

    private static int GetConstructorArgumentInt(CustomAttributeData? attribute, int index)
    {
        if (attribute is null || attribute.ConstructorArguments.Count <= index)
        {
            return 0;
        }

        return attribute.ConstructorArguments[index].Value is int intValue ? intValue : 0;
    }

    private static string? GetNamedArgumentString(CustomAttributeData? attribute, string name)
    {
        if (attribute is null)
        {
            return null;
        }

        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.MemberName, name, StringComparison.Ordinal))
            {
                return namedArgument.TypedValue.Value?.ToString();
            }
        }

        return null;
    }

    private static bool GetNamedArgumentBool(CustomAttributeData? attribute, string name)
    {
        if (attribute is null)
        {
            return false;
        }

        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.MemberName, name, StringComparison.Ordinal)
                && namedArgument.TypedValue.Value is bool boolValue)
            {
                return boolValue;
            }
        }

        return false;
    }

    private static (string? LongName, char? ShortName) ParseOptionNames(CustomAttributeData attribute, string memberName)
    {
        string? longName = null;
        char? shortName = null;
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.ArgumentType == typeof(string))
            {
                longName = argument.Value?.ToString();
            }
            else if (argument.ArgumentType == typeof(char) && argument.Value is char charValue)
            {
                shortName = charValue;
            }
        }

        return (longName ?? ConvertToKebabCase(TrimKnownSuffix(memberName)), shortName);
    }

    private static MemberInfo[] DeduplicateMembers(IEnumerable<MemberInfo> members)
        => members
            .GroupBy(static member => member.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();

    private static List<CapturedOption> DeduplicateOptions(IEnumerable<CapturedOption> options)
        => options
            .GroupBy(static option => option.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();

    private static bool LooksLikeHeuristicOptionMember(MemberInfo member, out Type memberType)
    {
        memberType = GetMemberType(member);
        if (string.IsNullOrWhiteSpace(member.Name)
            || member.Name.Contains("k__BackingField", StringComparison.Ordinal)
            || member.Name.StartsWith("_", StringComparison.Ordinal))
        {
            return false;
        }

        if (memberType == typeof(bool) || memberType == typeof(bool?))
        {
            return true;
        }

        return IsSimpleValueType(memberType) || IsSequenceType(memberType);
    }

    private static Type GetMemberType(MemberInfo member)
        => member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => typeof(object),
        };

    private static bool IsSequenceType(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        return type.IsArray
            || (type.IsGenericType && type.GetGenericTypeDefinition() is { } genericTypeDefinition
                && (genericTypeDefinition == typeof(IEnumerable<>)
                    || genericTypeDefinition == typeof(ICollection<>)
                    || genericTypeDefinition == typeof(IList<>)
                    || genericTypeDefinition == typeof(IReadOnlyCollection<>)
                    || genericTypeDefinition == typeof(IReadOnlyList<>)
                    || genericTypeDefinition == typeof(List<>)
                    || genericTypeDefinition == typeof(HashSet<>)));
    }

    private static bool IsBoolType(Type type)
        => Nullable.GetUnderlyingType(type) == typeof(bool) || type == typeof(bool);

    private static bool IsSimpleValueType(Type type)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        if (effectiveType.IsEnum)
        {
            return true;
        }

        return effectiveType == typeof(string)
            || effectiveType == typeof(bool)
            || effectiveType == typeof(byte)
            || effectiveType == typeof(short)
            || effectiveType == typeof(int)
            || effectiveType == typeof(long)
            || effectiveType == typeof(float)
            || effectiveType == typeof(double)
            || effectiveType == typeof(decimal)
            || effectiveType == typeof(Guid)
            || effectiveType == typeof(DateTime)
            || effectiveType == typeof(DateTimeOffset)
            || effectiveType == typeof(TimeSpan)
            || effectiveType == typeof(Uri);
    }

    private static List<string>? ReadAllowedValues(Type type)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        if (!effectiveType.IsEnum)
        {
            return null;
        }

        return Enum.GetNames(effectiveType).ToList();
    }

    private static string? FormatTypeName(Type type)
    {
        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        if (effectiveType == typeof(bool))
        {
            return "Boolean";
        }

        if (effectiveType == typeof(string))
        {
            return "String";
        }

        if (effectiveType == typeof(int))
        {
            return "Int32";
        }

        if (effectiveType == typeof(long))
        {
            return "Int64";
        }

        if (effectiveType == typeof(float))
        {
            return "Float";
        }

        if (effectiveType == typeof(double))
        {
            return "Double";
        }

        if (effectiveType == typeof(decimal))
        {
            return "Decimal";
        }

        if (effectiveType == typeof(DateTime))
        {
            return "DateTime";
        }

        if (effectiveType == typeof(DateTimeOffset))
        {
            return "DateTimeOffset";
        }

        if (effectiveType == typeof(TimeSpan))
        {
            return "TimeSpan";
        }

        if (effectiveType == typeof(Guid))
        {
            return "Guid";
        }

        if (effectiveType == typeof(Uri))
        {
            return "Uri";
        }

        if (effectiveType.IsEnum)
        {
            return effectiveType.Name;
        }

        if (type.IsArray)
        {
            return $"{FormatTypeName(type.GetElementType()!)}[]";
        }

        return effectiveType.Name;
    }

    private static string TrimKnownSuffix(string name)
    {
        if (name.EndsWith("Option", StringComparison.OrdinalIgnoreCase) && name.Length > "Option".Length)
        {
            return name[..^"Option".Length];
        }

        if (name.EndsWith("Options", StringComparison.OrdinalIgnoreCase) && name.Length > "Options".Length)
        {
            return name[..^"Options".Length];
        }

        return name;
    }

    private static string ConvertToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "value";
        }

        var builder = new System.Text.StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '_')
            {
                builder.Append('-');
                continue;
            }

            if (char.IsUpper(character)
                && index > 0
                && !char.IsUpper(value[index - 1])
                && value[index - 1] != '-')
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string ConvertToArgumentName(string value)
        => ConvertToKebabCase(TrimKnownSuffix(value))
            .Replace('-', '_')
            .ToUpperInvariant();
}
