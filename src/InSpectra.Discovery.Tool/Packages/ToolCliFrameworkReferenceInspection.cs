namespace InSpectra.Discovery.Tool.Packages;

internal sealed record ToolCliFrameworkReferenceInspection(
    string FrameworkName,
    IReadOnlyList<string> ReferencingAssemblyPaths);
