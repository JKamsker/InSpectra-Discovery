namespace InSpectra.Discovery.Tool.Help.Documents;


internal sealed record Document(
    string? Title,
    string? Version,
    string? ApplicationDescription,
    string? CommandDescription,
    IReadOnlyList<string> UsageLines,
    IReadOnlyList<Item> Arguments,
    IReadOnlyList<Item> Options,
    IReadOnlyList<Item> Commands)
{
    public bool HasContent
        => UsageLines.Count > 0
            || Arguments.Count > 0
            || Options.Count > 0
            || Commands.Count > 0
            || !string.IsNullOrWhiteSpace(CommandDescription)
            || !string.IsNullOrWhiteSpace(ApplicationDescription);
}
