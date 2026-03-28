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

        return string.Equals(existingCliFramework, "CliFx", StringComparison.OrdinalIgnoreCase)
            && HasCliFx(candidateCliFramework)
            && !string.Equals(existingCliFramework, candidateCliFramework, StringComparison.OrdinalIgnoreCase);
    }
}
