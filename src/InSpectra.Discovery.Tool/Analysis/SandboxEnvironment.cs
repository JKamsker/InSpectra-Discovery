namespace InSpectra.Discovery.Tool.Analysis;

internal sealed record SandboxEnvironment(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<string> Directories);
