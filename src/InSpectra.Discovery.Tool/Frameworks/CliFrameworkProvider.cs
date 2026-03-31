namespace InSpectra.Discovery.Tool.Frameworks;

using InSpectra.Discovery.Tool.StaticAnalysis.Attributes;


internal sealed record CliFrameworkProvider(
    string Name,
    IReadOnlyList<string> LabelAliases,
    IReadOnlyList<string> DependencyIds,
    IReadOnlyList<string> PackageAssemblyNames,
    bool SupportsCliFxAnalysis,
    bool SupportsHookAnalysis,
    StaticAnalysisFrameworkAdapter? StaticAnalysisAdapter)
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

internal sealed record StaticAnalysisFrameworkAdapter(
    string FrameworkName,
    string AssemblyName,
    IStaticAttributeReader Reader);
