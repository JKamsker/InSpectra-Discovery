internal static class ToolHelpUsageArgumentInventorySupport
{
    public static bool LooksLikeCommandInventoryEcho(
        IReadOnlyList<ToolHelpItem> explicitArguments,
        IReadOnlyList<ToolHelpItem> commands)
    {
        if (explicitArguments.Count == 0 || commands.Count == 0 || explicitArguments.Count != commands.Count)
        {
            return false;
        }

        return explicitArguments.Zip(commands).All(pair =>
            string.Equals(
                NormalizeCommandInventoryKey(pair.First.Key),
                NormalizeCommandInventoryKey(pair.Second.Key),
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                NormalizeInlineText(pair.First.Description),
                NormalizeInlineText(pair.Second.Description),
                StringComparison.OrdinalIgnoreCase));
    }

    public static bool LooksLikeAuxiliaryInventoryEcho(
        IReadOnlyList<ToolHelpItem> explicitArguments,
        IReadOnlyList<string> usageLines)
    {
        if (explicitArguments.Count == 0)
        {
            return false;
        }

        var normalizedUsage = usageLines
            .Select(NormalizeInlineText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (normalizedUsage.Length == 0 || normalizedUsage.Any(line => !line.Contains("command", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return explicitArguments.All(argument => NormalizeCommandInventoryKey(argument.Key) == "command");
    }

    private static string NormalizeInlineText(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("`", string.Empty, StringComparison.Ordinal).Trim();

    private static string NormalizeCommandInventoryKey(string key)
        => NormalizeInlineText(key).Trim('[', ']', '<', '>', '(', ')').ToLowerInvariant();
}
