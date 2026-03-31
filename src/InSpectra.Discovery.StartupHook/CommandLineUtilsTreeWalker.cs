using System.Collections;
using System.Text.RegularExpressions;

internal static class CommandLineUtilsTreeWalker
{
    private static readonly Regex OptionNamePattern = new(
        @"(?<!\S)(?:--?[A-Za-z0-9][A-Za-z0-9-]*|/[A-Za-z0-9?][A-Za-z0-9?-]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static CapturedCommand Walk(object commandLineApplication)
    {
        var captured = new CapturedCommand
        {
            Name = ReflectionValueReader.GetMemberValue<string>(commandLineApplication, "Name"),
            Description = ReflectionValueReader.GetMemberValue<string>(commandLineApplication, "Description"),
            IsHidden = IsHidden(commandLineApplication),
        };

        foreach (var alias in ResolveCommandAliases(commandLineApplication, captured.Name))
        {
            captured.Aliases.Add(alias);
        }

        var options = ReflectionValueReader.GetEnumerable<object>(commandLineApplication, "Options").ToList();
        AddBuiltInOptionIfMissing(options, ReflectionValueReader.GetMemberValue(commandLineApplication, "OptionHelp"));
        AddBuiltInOptionIfMissing(options, ReflectionValueReader.GetMemberValue(commandLineApplication, "OptionVersion"));
        foreach (var option in options)
        {
            captured.Options.Add(WalkOption(option));
        }

        foreach (var argument in ReflectionValueReader.GetEnumerable<object>(commandLineApplication, "Arguments"))
        {
            captured.Arguments.Add(WalkArgument(argument));
        }

        foreach (var subcommand in ReflectionValueReader.GetEnumerable<object>(commandLineApplication, "Commands"))
        {
            captured.Subcommands.Add(Walk(subcommand));
        }

        return captured;
    }

    private static CapturedOption WalkOption(object option)
    {
        var optionNames = ResolveOptionNames(option);
        var primaryName = optionNames.FirstOrDefault(static name => name.StartsWith("--", StringComparison.Ordinal))
            ?? optionNames.FirstOrDefault()
            ?? ReflectionValueReader.GetMemberValue<string>(option, "SymbolName")
            ?? "option";
        var captured = new CapturedOption
        {
            Name = primaryName,
            Description = ReflectionValueReader.GetMemberValue<string>(option, "Description"),
            IsHidden = IsHidden(option),
            Recursive = ReflectionValueReader.GetMemberValue<bool>(option, "Inherited"),
            ValueType = ResolveOptionValueType(option),
            ArgumentName = ResolveOptionArgumentName(option),
        };

        foreach (var alias in optionNames.Where(alias => !string.Equals(alias, primaryName, StringComparison.Ordinal)))
        {
            captured.Aliases.Add(alias);
        }

        (captured.MinArity, captured.MaxArity) = ResolveOptionArity(option);
        (captured.HasDefaultValue, captured.DefaultValue) = ReadDefaultValue(option);

        return captured;
    }

    private static CapturedArgument WalkArgument(object argument)
    {
        var multipleValues = ReflectionValueReader.GetMemberValue<bool>(argument, "MultipleValues");
        var maxArity = multipleValues ? int.MaxValue : 1;
        var defaultValue = ReadDefaultValue(argument);

        return new CapturedArgument
        {
            Name = ReflectionValueReader.GetMemberValue<string>(argument, "Name"),
            Description = ReflectionValueReader.GetMemberValue<string>(argument, "Description"),
            IsHidden = IsHidden(argument),
            MinArity = 1,
            MaxArity = maxArity,
            ValueType = "String",
            HasDefaultValue = defaultValue.HasDefault,
            DefaultValue = defaultValue.DefaultValue,
        };
    }

    private static IEnumerable<string> ResolveCommandAliases(object commandLineApplication, string? primaryName)
        => ReflectionValueReader.GetEnumerable<string>(commandLineApplication, "Names")
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.Ordinal)
            .Where(alias => !string.Equals(alias, primaryName, StringComparison.Ordinal));

    private static IReadOnlyList<string> ResolveOptionNames(object option)
    {
        var names = new List<string>();
        var template = ReflectionValueReader.GetMemberValue<string>(option, "Template");
        if (!string.IsNullOrWhiteSpace(template))
        {
            var templateText = template!;
            foreach (var match in OptionNamePattern.Matches(templateText).Cast<Match>())
            {
                if (!string.IsNullOrWhiteSpace(match.Value))
                {
                    names.Add(match.Value);
                }
            }
        }

        AddOptionName(names, ReflectionValueReader.GetMemberValue<string>(option, "LongName"), "--");
        AddOptionName(names, ReflectionValueReader.GetMemberValue<string>(option, "ShortName"), "-");
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ResolveOptionArgumentName(object option)
    {
        var optionType = ReflectionValueReader.GetMemberValue(option, "OptionType")?.ToString();
        if (string.Equals(optionType, "NoValue", StringComparison.Ordinal))
        {
            return null;
        }

        var valueName = ReflectionValueReader.GetMemberValue<string>(option, "ValueName");
        if (!string.IsNullOrWhiteSpace(valueName))
        {
            return valueName.ToUpperInvariant();
        }

        var primaryName = ResolveOptionNames(option).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(primaryName))
        {
            return "VALUE";
        }

        return primaryName.TrimStart('-', '/').Replace('-', '_').ToUpperInvariant();
    }

    private static string? ResolveOptionValueType(object option)
    {
        var optionType = ReflectionValueReader.GetMemberValue(option, "OptionType")?.ToString();
        return optionType switch
        {
            "NoValue" => "Boolean",
            "SingleOrNoValue" => "String",
            "SingleValue" => "String",
            "MultipleValue" => "String[]",
            _ => null,
        };
    }

    private static (int Min, int Max) ResolveOptionArity(object option)
    {
        var optionType = ReflectionValueReader.GetMemberValue(option, "OptionType")?.ToString();
        return optionType switch
        {
            "NoValue" => (0, 0),
            "SingleOrNoValue" => (0, 1),
            "SingleValue" => (1, 1),
            "MultipleValue" => (1, int.MaxValue),
            _ => (0, 0),
        };
    }

    private static (bool HasDefault, string? DefaultValue) ReadDefaultValue(object source)
    {
        var defaultValue = ReflectionValueReader.GetMemberValue<string>(source, "DefaultValue");
        return string.IsNullOrWhiteSpace(defaultValue)
            ? (false, null)
            : (true, defaultValue);
    }

    private static bool IsHidden(object source)
    {
        var isHidden = ReflectionValueReader.GetMemberValue(source, "Hidden")
            ?? ReflectionValueReader.GetMemberValue(source, "IsHidden");
        if (isHidden is bool hidden)
        {
            return hidden;
        }

        var showInHelpText = ReflectionValueReader.GetMemberValue(source, "ShowInHelpText");
        return showInHelpText is bool visible && !visible;
    }

    private static void AddBuiltInOptionIfMissing(List<object> options, object? option)
    {
        if (option is null)
        {
            return;
        }

        var optionNames = ResolveOptionNames(option);
        if (optionNames.Count == 0)
        {
            return;
        }

        foreach (var existingOption in options)
        {
            var existingNames = ResolveOptionNames(existingOption);
            if (existingNames.Intersect(optionNames, StringComparer.Ordinal).Any())
            {
                return;
            }
        }

        options.Add(option);
    }

    private static void AddOptionName(List<string> names, string? rawName, string prefix)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return;
        }

        var normalized = rawName.Trim();
        if (normalized.StartsWith("-", StringComparison.Ordinal) || normalized.StartsWith("/", StringComparison.Ordinal))
        {
            names.Add(normalized);
            return;
        }

        names.Add(prefix + normalized);
    }

}
