internal static class CliFrameworkSupport
{
    public static bool HasCliFx(string? cliFramework)
        => !string.IsNullOrWhiteSpace(cliFramework)
            && (string.Equals(cliFramework, "CliFx", StringComparison.OrdinalIgnoreCase)
                || cliFramework.StartsWith("CliFx + ", StringComparison.OrdinalIgnoreCase));

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
