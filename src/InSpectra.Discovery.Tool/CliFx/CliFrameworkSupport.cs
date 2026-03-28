internal static class CliFrameworkSupport
{
    public static bool HasCliFx(string? cliFramework)
        => !string.IsNullOrWhiteSpace(cliFramework)
            && (string.Equals(cliFramework, "CliFx", StringComparison.OrdinalIgnoreCase)
                || cliFramework.StartsWith("CliFx + ", StringComparison.OrdinalIgnoreCase));
}
