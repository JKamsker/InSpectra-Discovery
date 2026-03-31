namespace InSpectra.Discovery.Tool.Help.Documents;


internal sealed record Item(
    string Key,
    bool IsRequired,
    string? Description);
