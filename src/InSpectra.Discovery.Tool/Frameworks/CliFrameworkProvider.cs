namespace InSpectra.Discovery.Tool.Frameworks;


internal sealed record CliFrameworkProvider(
    string Name,
    IReadOnlyList<string> LabelAliases,
    IReadOnlyList<string> DependencyIds,
    IReadOnlyList<string> PackageAssemblyNames,
    IReadOnlyList<string> RuntimeAssemblyNames,
    bool SupportsCliFxAnalysis,
    bool SupportsHookAnalysis,
    bool SupportsStaticAnalysis)
{
    public bool Matches(IReadOnlySet<string> dependencyIds, IReadOnlySet<string> assemblyNames)
        => DependencyIds.Any(dependencyIds.Contains) || PackageAssemblyNames.Any(assemblyNames.Contains);

    public IEnumerable<string> EnumerateLabels()
    {
        yield return Name;

        foreach (var alias in LabelAliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                yield return alias;
            }
        }
    }
}
