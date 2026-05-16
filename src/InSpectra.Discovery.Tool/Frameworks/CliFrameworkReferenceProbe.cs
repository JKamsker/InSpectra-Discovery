namespace InSpectra.Discovery.Tool.Frameworks;

internal sealed record CliFrameworkReferenceProbe(
    string FrameworkName,
    IReadOnlyList<string> PackageAssemblyNames,
    IReadOnlyList<string> RuntimeAssemblyNames);
