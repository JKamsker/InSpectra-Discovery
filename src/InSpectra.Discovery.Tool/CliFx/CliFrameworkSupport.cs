internal static class CliFrameworkSupport
{
    public static bool HasCliFx(string? cliFramework)
        => !string.IsNullOrWhiteSpace(cliFramework)
            && cliFramework.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(part => string.Equals(part, "CliFx", StringComparison.OrdinalIgnoreCase));

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
