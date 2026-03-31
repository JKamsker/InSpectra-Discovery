namespace InSpectra.Discovery.Tool.Analysis.Execution;


internal sealed record SandboxEnvironment(
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<string> Directories);
