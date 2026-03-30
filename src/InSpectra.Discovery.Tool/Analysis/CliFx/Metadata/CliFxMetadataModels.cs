namespace InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;

internal sealed record CliFxCommandDefinition(
    string? Name,
    string? Description,
    IReadOnlyList<CliFxParameterDefinition> Parameters,
    IReadOnlyList<CliFxOptionDefinition> Options);

internal sealed record CliFxParameterDefinition(
    int Order,
    string Name,
    bool IsRequired,
    bool IsSequence,
    string? ClrType,
    string? Description,
    IReadOnlyList<string> AcceptedValues);

internal sealed record CliFxOptionDefinition(
    string? Name,
    char? ShortName,
    bool IsRequired,
    bool IsSequence,
    bool IsBoolLike,
    string? ClrType,
    string? Description,
    string? EnvironmentVariable,
    IReadOnlyList<string> AcceptedValues,
    string? ValueName = null);

internal sealed record CliFxHelpDocument(
    string? Title,
    string? Version,
    string? ApplicationDescription,
    string? CommandDescription,
    IReadOnlyList<string> UsageLines,
    IReadOnlyList<CliFxHelpItem> Parameters,
    IReadOnlyList<CliFxHelpItem> Options,
    IReadOnlyList<CliFxHelpItem> Commands);

internal sealed record CliFxHelpItem(
    string Key,
    bool IsRequired,
    string? Description);

