namespace InSpectra.Discovery.Tool.Help;

internal sealed record Item(
    string Key,
    bool IsRequired,
    string? Description);
