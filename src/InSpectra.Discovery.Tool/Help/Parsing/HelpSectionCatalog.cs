namespace InSpectra.Discovery.Tool.Help.Parsing;

internal static class HelpSectionCatalog
{
    public static IReadOnlyDictionary<string, string> Aliases { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ARGUMENT"] = "arguments",
        ["ARGUMENTE"] = "arguments",
        ["ARGUMENTS"] = "arguments",
        ["BEFEHL"] = "commands",
        ["BEFEHLE"] = "commands",
        ["COMMAND"] = "commands",
        ["COMMAND LIST"] = "commands",
        ["COMMANDS"] = "commands",
        ["DESCRIPTION"] = "description",
        ["EXAMPLE"] = "examples",
        ["EXAMPLES"] = "examples",
        ["FLAGS"] = "options",
        ["KOMMANDO"] = "commands",
        ["KOMMANDOS"] = "commands",
        ["OPTION"] = "options",
        ["OPTIONEN"] = "options",
        ["OPTIONS"] = "options",
        ["PARAMETER"] = "arguments",
        ["PARAMETERS"] = "arguments",
        ["SUBCOMMANDS"] = "commands",
        ["SYNOPSIS"] = "usage",
        ["USAGE"] = "usage",
        ["VERBS"] = "commands",
        ["VERWENDUNG"] = "usage",
    };

    public static IReadOnlySet<string> IgnoredHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "RAW OUTPUT",
        "REDIRECTION WARNING",
    };

    public static bool TryResolveAlias(string alias, out string sectionName)
    {
        if (Aliases.TryGetValue(alias, out var resolvedSectionName))
        {
            sectionName = resolvedSectionName;
            return true;
        }

        if (alias.EndsWith("OPTIONS", StringComparison.OrdinalIgnoreCase)
            || alias.EndsWith("OPTIONEN", StringComparison.OrdinalIgnoreCase))
        {
            sectionName = "options";
            return true;
        }

        if (alias.EndsWith("ARGUMENTS", StringComparison.OrdinalIgnoreCase)
            || alias.EndsWith("ARGUMENTE", StringComparison.OrdinalIgnoreCase)
            || alias.EndsWith("PARAMETERS", StringComparison.OrdinalIgnoreCase)
            || alias.EndsWith("PARAMETER", StringComparison.OrdinalIgnoreCase))
        {
            sectionName = "arguments";
            return true;
        }

        sectionName = string.Empty;
        return false;
    }
}
