using System.Collections;
using System.Reflection;

internal static class CommandTreeWalker
{
    public static CapturedCommand Walk(object command, Assembly sclAssembly)
    {
        var commandType = command.GetType();
        var captured = new CapturedCommand
        {
            Name = GetProperty<string>(command, "Name"),
            Description = GetProperty<string>(command, "Description"),
            IsHidden = GetProperty<bool>(command, "IsHidden"),
        };

        // Aliases
        var aliases = GetProperty<IEnumerable>(command, "Aliases");
        if (aliases is not null)
        {
            foreach (var alias in aliases)
            {
                if (alias is string s)
                    captured.Aliases.Add(s);
            }
        }

        // Options
        var options = GetProperty<IEnumerable>(command, "Options");
        if (options is not null)
        {
            foreach (var option in options)
                captured.Options.Add(WalkOption(option));
        }

        // Arguments
        var arguments = GetProperty<IEnumerable>(command, "Arguments");
        if (arguments is not null)
        {
            foreach (var argument in arguments)
                captured.Arguments.Add(WalkArgument(argument));
        }

        // Subcommands - property name varies: "Subcommands" (newer) or "Children" (older).
        var subcommands = GetProperty<IEnumerable>(command, "Subcommands")
                       ?? GetProperty<IEnumerable>(command, "Children");
        if (subcommands is not null)
        {
            // "Children" may include Options and Arguments mixed in; filter to Command types.
            var commandBaseType = sclAssembly.GetType("System.CommandLine.Command");
            foreach (var child in subcommands)
            {
                if (commandBaseType is not null && commandBaseType.IsInstanceOfType(child))
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
            IsRequired = GetProperty<bool>(option, "IsRequired"),
            IsHidden = GetProperty<bool>(option, "IsHidden"),
            Recursive = GetProperty<bool>(option, "Recursive"),
        };

        // Aliases
        var aliases = GetProperty<IEnumerable>(option, "Aliases");
        if (aliases is not null)
        {
            foreach (var alias in aliases)
            {
                if (alias is string s)
                    captured.Aliases.Add(s);
            }
        }

        // ValueType - may be directly on Option or on its Argument property.
        var valueType = GetProperty<Type>(option, "ValueType");
        if (valueType is null)
        {
            var argument = GetProperty<object>(option, "Argument");
            if (argument is not null)
                valueType = GetProperty<Type>(argument, "ValueType");
        }
        captured.ValueType = valueType?.Name;

        // Arity
        ReadArity(option, captured);

        return captured;
    }

    private static CapturedArgument WalkArgument(object argument)
    {
        var captured = new CapturedArgument
        {
            Name = GetProperty<string>(argument, "Name"),
            Description = GetProperty<string>(argument, "Description"),
            IsHidden = GetProperty<bool>(argument, "IsHidden"),
        };

        var valueType = GetProperty<Type>(argument, "ValueType");
        captured.ValueType = valueType?.Name;

        ReadArity(argument, captured);

        return captured;
    }

    private static void ReadArity(object source, CapturedOption target)
    {
        var arity = GetProperty<object>(source, "Arity");
        if (arity is null) return;
        target.MinArity = GetProperty<int>(arity, "MinimumNumberOfValues");
        target.MaxArity = GetProperty<int>(arity, "MaximumNumberOfValues");
    }

    private static void ReadArity(object source, CapturedArgument target)
    {
        var arity = GetProperty<object>(source, "Arity");
        if (arity is null) return;
        target.MinArity = GetProperty<int>(arity, "MinimumNumberOfValues");
        target.MaxArity = GetProperty<int>(arity, "MaximumNumberOfValues");
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
