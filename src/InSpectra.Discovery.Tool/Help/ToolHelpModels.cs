internal sealed record ToolHelpDocument(
    string? Title,
    string? Version,
    string? ApplicationDescription,
    string? CommandDescription,
    IReadOnlyList<string> UsageLines,
    IReadOnlyList<ToolHelpItem> Arguments,
    IReadOnlyList<ToolHelpItem> Options,
    IReadOnlyList<ToolHelpItem> Commands)
{
    public bool HasContent
        => UsageLines.Count > 0
            || Arguments.Count > 0
            || Options.Count > 0
            || Commands.Count > 0
            || !string.IsNullOrWhiteSpace(CommandDescription)
            || !string.IsNullOrWhiteSpace(ApplicationDescription);
}

internal sealed record ToolHelpItem(
    string Key,
    bool IsRequired,
    string? Description);
