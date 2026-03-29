internal static class CliFrameworkSupport
{
    public static bool HasCliFx(string? cliFramework)
        => !string.IsNullOrWhiteSpace(cliFramework)
            && cliFramework.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(part => string.Equals(part, "CliFx", StringComparison.OrdinalIgnoreCase));

    private static readonly HashSet<string> StaticAnalysisFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        "CommandLineParser",
        "System.CommandLine",
        "McMaster.Extensions.CommandLineUtils",
        "Microsoft.Extensions.CommandLineUtils",
        "Argu",
        "Cocona",
        "DocoptNet",
        "ConsoleAppFramework",
        "CommandDotNet",
        "PowerArgs",
        "Oakton",
        "ManyConsole",
        "Mono.Options / NDesk.Options",
        "Mono.Options",
        "NDesk.Options",
    };

    public static bool HasStaticAnalysisSupport(string? cliFramework)
        => !string.IsNullOrWhiteSpace(cliFramework)
            && cliFramework.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(StaticAnalysisFrameworks.Contains);

    public static bool ShouldReplace(string? existingCliFramework, string? candidateCliFramework)
    {
        if (string.IsNullOrWhiteSpace(candidateCliFramework))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(existingCliFramework))
        {
            return true;
        }

        if (string.Equals(existingCliFramework, candidateCliFramework, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!HasCliFx(candidateCliFramework))
        {
            return false;
        }

        return !HasCliFx(existingCliFramework)
            || string.Equals(existingCliFramework, "CliFx", StringComparison.OrdinalIgnoreCase);
    }
}
