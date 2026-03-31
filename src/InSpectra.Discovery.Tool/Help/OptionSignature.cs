namespace InSpectra.Discovery.Tool.Help;

internal sealed record OptionSignature(
    string? PrimaryName,
    IReadOnlyList<string> Aliases,
    string? ArgumentName,
    bool ArgumentRequired);
