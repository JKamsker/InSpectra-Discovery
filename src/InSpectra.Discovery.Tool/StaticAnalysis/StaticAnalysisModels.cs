namespace InSpectra.Discovery.Tool.StaticAnalysis;

internal sealed record StaticCommandDefinition(
    string? Name,
    string? Description,
    bool IsDefault,
    bool IsHidden,
    IReadOnlyList<StaticValueDefinition> Values,
    IReadOnlyList<StaticOptionDefinition> Options);

internal sealed record StaticOptionDefinition(
    string? LongName,
    char? ShortName,
    bool IsRequired,
    bool IsSequence,
    bool IsBoolLike,
    string? ClrType,
    string? Description,
    string? DefaultValue,
    string? MetaValue,
    IReadOnlyList<string> AcceptedValues,
    string? PropertyName = null);

internal sealed record StaticValueDefinition(
    int Index,
    string? Name,
    bool IsRequired,
    bool IsSequence,
    string? ClrType,
    string? Description,
    string? DefaultValue,
    IReadOnlyList<string> AcceptedValues);

