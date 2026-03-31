namespace InSpectra.Discovery.Tool.Help;

internal sealed record CommandNode(
    string FullName,
    string DisplayName,
    string? Description)
{
    public IReadOnlyList<CommandNode> Children { get; init; } = [];
}
