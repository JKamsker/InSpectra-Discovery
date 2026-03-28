internal static class CliFrameworkCatalogClassifier
{
    private static readonly IReadOnlyList<FrameworkSignature> Signatures =
    [
        new("Spectre.Console.Cli", ["Spectre.Console.Cli"], ["Spectre.Console.Cli.dll"]),
        new("CliFx", ["CliFx"], ["CliFx.dll"]),
        new("System.CommandLine", ["System.CommandLine"], ["System.CommandLine.dll"]),
        new("McMaster.Extensions.CommandLineUtils", ["McMaster.Extensions.CommandLineUtils"], ["McMaster.Extensions.CommandLineUtils.dll"]),
        new("Microsoft.Extensions.CommandLineUtils", ["Microsoft.Extensions.CommandLineUtils"], ["Microsoft.Extensions.CommandLineUtils.dll"]),
        new("Argu", ["Argu"], ["Argu.dll"]),
        new("Cocona", ["Cocona"], ["Cocona.dll"]),
        new("DocoptNet", ["DocoptNet"], ["DocoptNet.dll"]),
        new("ConsoleAppFramework", ["ConsoleAppFramework"], ["ConsoleAppFramework.dll"]),
        new("CommandDotNet", ["CommandDotNet"], ["CommandDotNet.dll"]),
        new("PowerArgs", ["PowerArgs"], ["PowerArgs.dll"]),
        new("Oakton", ["Oakton"], ["Oakton.dll"]),
        new("ManyConsole", ["ManyConsole"], ["ManyConsole.dll"]),
        new("CommandLineParser", ["CommandLineParser"], ["CommandLine.dll"]),
        new("Mono.Options / NDesk.Options", ["Mono.Options", "NDesk.Options"], ["Mono.Options.dll", "NDesk.Options.dll"]),
    ];

    public static string? Detect(CatalogLeaf catalogLeaf)
    {
        var dependencyIds = (catalogLeaf.DependencyGroups ?? [])
            .SelectMany(group => group.Dependencies ?? [])
            .Select(dependency => dependency.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var assemblyNames = (catalogLeaf.PackageEntries ?? [])
            .Select(entry => entry.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matches = Signatures
            .Where(signature => signature.Matches(dependencyIds, assemblyNames))
            .Select(signature => signature.Name)
            .ToArray();

        return matches.Length == 0
            ? null
            : string.Join(" + ", matches);
    }

    private sealed record FrameworkSignature(
        string Name,
        IReadOnlyList<string> DependencyIds,
        IReadOnlyList<string> AssemblyNames)
    {
        public bool Matches(IReadOnlySet<string> dependencyIds, IReadOnlySet<string> assemblyNames)
            => DependencyIds.Any(dependencyIds.Contains) || AssemblyNames.Any(assemblyNames.Contains);
    }
}
