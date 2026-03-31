namespace InSpectra.Discovery.Tool.Frameworks;

using InSpectra.Discovery.Tool.StaticAnalysis.Attributes;

using InSpectra.Discovery.Tool.NuGet;

internal static class CliFrameworkProviderRegistry
{
    private static readonly IReadOnlyList<CliFrameworkProvider> Providers = CreateProviders();
    private static readonly IReadOnlyDictionary<string, CliFrameworkProvider> ProvidersByLabel = CreateProvidersByLabel(Providers);

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

        var matches = Providers
            .Where(provider => provider.Matches(dependencyIds, assemblyNames))
            .Select(provider => provider.Name)
            .ToArray();

        return matches.Length == 0
            ? null
            : string.Join(" + ", matches);
    }

    public static bool HasCliFxAnalysisSupport(string? cliFramework)
        => ResolveProviders(cliFramework).Any(static provider => provider.SupportsCliFxAnalysis);

    public static bool HasStaticAnalysisSupport(string? cliFramework)
        => ResolveStaticAnalysisAdapter(cliFramework) is not null;

    public static bool HasHookAnalysisSupport(string? cliFramework)
        => ResolveProviders(cliFramework).Any(static provider => provider.SupportsHookAnalysis);

    public static string? ResolveHookAnalysisFramework(string? cliFramework)
        => ResolveProviders(cliFramework)
            .Where(static provider => provider.SupportsHookAnalysis)
            .Select(static provider => provider.Name)
            .FirstOrDefault();

    public static StaticAnalysisFrameworkAdapter? ResolveStaticAnalysisAdapter(string? cliFramework)
    {
        foreach (var provider in ResolveProviders(cliFramework))
        {
            if (provider.StaticAnalysisAdapter is not null)
            {
                return provider.StaticAnalysisAdapter;
            }
        }

        return null;
    }

    public static bool ShouldReplace(string? existingCliFramework, string? candidateCliFramework)
    {
        if (string.IsNullOrWhiteSpace(candidateCliFramework))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(existingCliFramework))
        {
            return true;
        }

        if (string.Equals(existingCliFramework, candidateCliFramework, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!HasCliFxAnalysisSupport(candidateCliFramework))
        {
            return false;
        }

        return !HasCliFxAnalysisSupport(existingCliFramework)
            || string.Equals(existingCliFramework, "CliFx", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<CliFrameworkProvider> ResolveProviders(string? cliFramework)
    {
        if (string.IsNullOrWhiteSpace(cliFramework))
        {
            yield break;
        }

        foreach (var part in cliFramework.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ProvidersByLabel.TryGetValue(part, out var provider))
            {
                yield return provider;
            }
        }
    }

    private static IReadOnlyList<CliFrameworkProvider> CreateProviders()
    {
        return
        [
            CreateCatalogOnlyProvider("Spectre.Console.Cli", ["Spectre.Console.Cli"], ["Spectre.Console.Cli.dll"]),
            CreateCliFxProvider(),
            CreateStaticAnalysisProvider("System.CommandLine", ["System.CommandLine"], ["System.CommandLine.dll"], "System.CommandLine", new SystemCommandLineAttributeReader()),
            CreateStaticAnalysisProvider("McMaster.Extensions.CommandLineUtils", ["McMaster.Extensions.CommandLineUtils"], ["McMaster.Extensions.CommandLineUtils.dll"], "McMaster.Extensions.CommandLineUtils", new McMasterAttributeReader()),
            CreateStaticAnalysisProvider("Microsoft.Extensions.CommandLineUtils", ["Microsoft.Extensions.CommandLineUtils"], ["Microsoft.Extensions.CommandLineUtils.dll"], "Microsoft.Extensions.CommandLineUtils", new McMasterAttributeReader()),
            CreateStaticAnalysisProvider("Argu", ["Argu"], ["Argu.dll"], "Argu", new ArguAttributeReader()),
            CreateStaticAnalysisProvider("Cocona", ["Cocona"], ["Cocona.dll"], "Cocona", new CoconaAttributeReader()),
            CreateCatalogOnlyProvider("DocoptNet", ["DocoptNet"], ["DocoptNet.dll"]),
            CreateCatalogOnlyProvider("ConsoleAppFramework", ["ConsoleAppFramework"], ["ConsoleAppFramework.dll"]),
            CreateStaticAnalysisProvider("CommandDotNet", ["CommandDotNet"], ["CommandDotNet.dll"], "CommandDotNet", new CommandDotNetAttributeReader()),
            CreateStaticAnalysisProvider("PowerArgs", ["PowerArgs"], ["PowerArgs.dll"], "PowerArgs", new PowerArgsAttributeReader()),
            CreateCatalogOnlyProvider("Oakton", ["Oakton"], ["Oakton.dll"]),
            CreateCatalogOnlyProvider("ManyConsole", ["ManyConsole"], ["ManyConsole.dll"]),
            CreateStaticAnalysisProvider("CommandLineParser", ["CommandLineParser"], ["CommandLine.dll"], "CommandLine", new CmdParserAttributeReader()),
            CreateCatalogOnlyProvider("Mono.Options / NDesk.Options", ["Mono.Options", "NDesk.Options"], ["Mono.Options.dll", "NDesk.Options.dll"], "Mono.Options", "NDesk.Options"),
        ];
    }

    private static IReadOnlyDictionary<string, CliFrameworkProvider> CreateProvidersByLabel(IReadOnlyList<CliFrameworkProvider> providers)
    {
        var lookup = new Dictionary<string, CliFrameworkProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            foreach (var label in provider.EnumerateLabels())
            {
                lookup[label] = provider;
            }
        }

        return lookup;
    }

    private static CliFrameworkProvider CreateCliFxProvider()
        => new(
            Name: "CliFx",
            LabelAliases: [],
            DependencyIds: ["CliFx"],
            PackageAssemblyNames: ["CliFx.dll"],
            SupportsCliFxAnalysis: true,
            SupportsHookAnalysis: false,
            StaticAnalysisAdapter: null);

    private static CliFrameworkProvider CreateCatalogOnlyProvider(
        string name,
        IReadOnlyList<string> dependencyIds,
        IReadOnlyList<string> packageAssemblyNames,
        params string[] labelAliases)
        => new(
            Name: name,
            LabelAliases: labelAliases,
            DependencyIds: dependencyIds,
            PackageAssemblyNames: packageAssemblyNames,
            SupportsCliFxAnalysis: false,
            SupportsHookAnalysis: false,
            StaticAnalysisAdapter: null);

    private static CliFrameworkProvider CreateStaticAnalysisProvider(
        string name,
        IReadOnlyList<string> dependencyIds,
        IReadOnlyList<string> packageAssemblyNames,
        string staticAssemblyName,
        IStaticAttributeReader reader,
        params string[] labelAliases)
        => new(
            Name: name,
            LabelAliases: labelAliases,
            DependencyIds: dependencyIds,
            PackageAssemblyNames: packageAssemblyNames,
            SupportsCliFxAnalysis: false,
            SupportsHookAnalysis:
                string.Equals(name, "System.CommandLine", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "McMaster.Extensions.CommandLineUtils", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Microsoft.Extensions.CommandLineUtils", StringComparison.OrdinalIgnoreCase),
            StaticAnalysisAdapter: new StaticAnalysisFrameworkAdapter(name, staticAssemblyName, reader));
}
