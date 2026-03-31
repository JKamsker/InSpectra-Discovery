using System.Collections;
using System.Reflection;

internal static class CommandTreeWalker
{
    public static CapturedCommand Walk(object command, Assembly sclAssembly)
    {
        var captured = new CapturedCommand
        {
            Name = GetProperty<string>(command, "Name"),
            Description = GetProperty<string>(command, "Description"),
            IsHidden = GetProperty<bool>(command, "Hidden") || GetProperty<bool>(command, "IsHidden"),
        };

        // Aliases
        foreach (var alias in GetEnumerable<string>(command, "Aliases"))
            captured.Aliases.Add(alias);

        // Options
        foreach (var option in GetEnumerable<object>(command, "Options"))
            captured.Options.Add(WalkOption(option));

        // Arguments
        foreach (var argument in GetEnumerable<object>(command, "Arguments"))
            captured.Arguments.Add(WalkArgument(argument));

        // Subcommands - property name varies: "Subcommands" (newer) or "Children" (older).
        var subcommands = GetProperty<IEnumerable>(command, "Subcommands")
                       ?? GetProperty<IEnumerable>(command, "Children");
        if (subcommands is not null)
        {
            var commandBaseType = sclAssembly.GetType("System.CommandLine.Command");
            foreach (var child in subcommands)
            {
                if (child is not null && commandBaseType is not null && commandBaseType.IsInstanceOfType(child))
                    captured.Subcommands.Add(Walk(child, sclAssembly));
            }
        }

        return captured;
    }

    private static CapturedOption WalkOption(object option)
    {
        var captured = new CapturedOption
        {
            Name = GetProperty<string>(option, "Name"),
            Description = GetProperty<string>(option, "Description"),
            // S.CL 2.0.5+ uses "Required" and "Hidden" (not "IsRequired"/"IsHidden").
            // Try both for cross-version compatibility.
            IsRequired = GetProperty<bool>(option, "Required") || GetProperty<bool>(option, "IsRequired"),
            IsHidden = GetProperty<bool>(option, "Hidden") || GetProperty<bool>(option, "IsHidden"),
            Recursive = GetProperty<bool>(option, "Recursive"),
        };

        // Aliases
        foreach (var alias in GetEnumerable<string>(option, "Aliases"))
            captured.Aliases.Add(alias);

        // ValueType — directly on Option in 2.0.5+, or on inner Argument in beta.
        var valueType = GetProperty<Type>(option, "ValueType");
        var innerArgument = GetProperty<object>(option, "Argument"); // beta only
        if (valueType is null && innerArgument is not null)
            valueType = GetProperty<Type>(innerArgument, "ValueType");
        captured.ValueType = FormatTypeName(valueType);

        // Argument display name (e.g., "SERVER", "COUNT").
        // S.CL 2.0.5+: "HelpName" property on Option.
        // Beta: inner Argument.Name.
        // Fallback: synthesize from option name (--server → SERVER).
        captured.ArgumentName = GetProperty<string>(option, "HelpName")
                             ?? (innerArgument is not null ? GetProperty<string>(innerArgument, "Name") : null)
                             ?? SynthesizeArgumentName(captured.Name, captured.ValueType);

        // Default values — try option directly first, then inner argument.
        (captured.HasDefaultValue, captured.DefaultValue) = ReadDefaultValue(option);
        if (!captured.HasDefaultValue && innerArgument is not null)
            (captured.HasDefaultValue, captured.DefaultValue) = ReadDefaultValue(innerArgument);

        captured.AllowedValues = ReadAllowedValues(option, valueType)
                              ?? (innerArgument is not null ? ReadAllowedValues(innerArgument, valueType) : null);

        // Arity
        (captured.MinArity, captured.MaxArity) = ReadArity(option);

        return captured;
    }

    private static string? SynthesizeArgumentName(string? optionName, string? valueType)
    {
        // Don't synthesize for flags (Boolean/Void).
        if (valueType is null or "Void" or "Boolean") return null;

        // --server → SERVER, --output-root → OUTPUT_ROOT
        if (optionName is null) return null;
        return optionName.TrimStart('-').Replace('-', '_').ToUpperInvariant();
    }

    private static CapturedArgument WalkArgument(object argument)
    {
        var valueType = GetProperty<Type>(argument, "ValueType");
        var captured = new CapturedArgument
        {
            Name = GetProperty<string>(argument, "Name"),
            Description = GetProperty<string>(argument, "Description"),
            IsHidden = GetProperty<bool>(argument, "Hidden") || GetProperty<bool>(argument, "IsHidden"),
            ValueType = FormatTypeName(valueType),
        };

        (captured.MinArity, captured.MaxArity) = ReadArity(argument);
        (captured.HasDefaultValue, captured.DefaultValue) = ReadDefaultValue(argument);
        captured.AllowedValues = ReadAllowedValues(argument, valueType);

        return captured;
    }

    private static (int Min, int Max) ReadArity(object source)
    {
        var arity = GetProperty<object>(source, "Arity");
        if (arity is null) return (0, 0);
        return (GetProperty<int>(arity, "MinimumNumberOfValues"), GetProperty<int>(arity, "MaximumNumberOfValues"));
    }

    private static (bool HasDefault, string? DefaultValue) ReadDefaultValue(object source)
    {
        try
        {
            var hasDefault = GetProperty<bool>(source, "HasDefaultValue");
            if (!hasDefault) return (false, null);

            var method = source.GetType().GetMethod("GetDefaultValue",
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method is not null)
            {
                var value = method.Invoke(source, null);
                return (true, value?.ToString());
            }

            return (true, null);
        }
        catch
        {
            return (false, null);
        }
    }

    private static List<string>? ReadAllowedValues(object source, Type? valueType)
    {
        // If the value type is an enum, extract all enum names as allowed values.
        if (valueType is null || !valueType.IsEnum) return null;

        try
        {
            return Enum.GetNames(valueType).ToList();
        }
        catch
        {
            return null;
        }
    }

    private static string? FormatTypeName(Type? type)
    {
        if (type is null) return null;

        // Unwrap Nullable<T>
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null) type = underlying;

        // Friendly names for common types
        return type.Name switch
        {
            "String" => "String",
            "Int32" => "Int32",
            "Int64" => "Int64",
            "Boolean" => "Boolean",
            "Double" => "Double",
            "Single" => "Float",
            "Decimal" => "Decimal",
            "DateTime" => "DateTime",
            "DateTimeOffset" => "DateTimeOffset",
            "TimeSpan" => "TimeSpan",
            "Guid" => "Guid",
            "Uri" => "Uri",
            "FileInfo" => "FileInfo",
            "DirectoryInfo" => "DirectoryInfo",
            _ when type.IsEnum => type.Name,
            _ when type.IsArray => $"{FormatTypeName(type.GetElementType())}[]",
            _ => type.Name,
        };
    }

    private static IEnumerable<T> GetEnumerable<T>(object obj, string name)
    {
        var enumerable = GetProperty<IEnumerable>(obj, name);
        if (enumerable is null) yield break;
        foreach (var item in enumerable)
        {
            if (item is T t) yield return t;
        }
    }

    private static T? GetProperty<T>(object obj, string name)
    {
        try
        {
            var prop = obj.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (prop is null) return default;
            var value = prop.GetValue(obj);
            return value is T t ? t : default;
        }
        catch
        {
            return default;
        }
    }

}
