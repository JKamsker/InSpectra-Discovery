namespace InSpectra.Discovery.Tool.Help.Signatures;


internal sealed record OptionSignature(
    string? PrimaryName,
    IReadOnlyList<string> Aliases,
    string? ArgumentName,
    bool ArgumentRequired);
