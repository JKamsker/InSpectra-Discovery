namespace InSpectra.Discovery.Tool.Analysis.Tools;

using InSpectra.Discovery.Tool.Frameworks;

using InSpectra.Discovery.Tool.Packages;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.Infrastructure.Host;

using InSpectra.Discovery.Tool.Catalog.Filtering.SpectreConsole;

using InSpectra.Discovery.Tool.NuGet;

internal interface IToolDescriptorResolver
{
    Task<ToolDescriptor> ResolveAsync(string packageId, string version, CancellationToken cancellationToken);
}

internal sealed class ToolDescriptorResolver : IToolDescriptorResolver
{
    public async Task<ToolDescriptor> ResolveAsync(string packageId, string version, CancellationToken cancellationToken)
    {
        using var scope = Runtime.CreateNuGetApiClientScope();
        var (leaf, catalogLeaf) = await PackageVersionResolver.ResolveAsync(scope.Client, packageId, version, cancellationToken);
        var packageInspection = await new PackageArchiveInspector(scope.Client).InspectAsync(leaf.PackageContent, cancellationToken);
        var cliFramework = DetectCliFramework(catalogLeaf, packageInspection);
        var (preferredMode, reason) = SelectMode(catalogLeaf, packageInspection, cliFramework);

        return new ToolDescriptor(
            packageId,
            version,
            packageInspection.ToolCommandNames.FirstOrDefault(),
            cliFramework,
            preferredMode,
            reason,
            $"https://www.nuget.org/packages/{packageId}/{version}",
            leaf.PackageContent,
            leaf.CatalogEntryUrl,
            PackageTitle: catalogLeaf.Title,
            PackageDescription: catalogLeaf.Description);
    }

    internal static ToolDescriptor ResolveFromCatalogLeaf(
        string packageId,
        string version,
        CatalogLeaf catalogLeaf,
        string? packageUrl,
        string? packageContentUrl,
        string? catalogEntryUrl)
    {
        var cliFramework = DetectCliFramework(catalogLeaf, packageInspection: null);
        var (preferredMode, reason) = SelectMode(catalogLeaf, packageInspection: null, cliFramework);

        return new ToolDescriptor(
            packageId,
            version,
            CommandName: null,
            cliFramework,
            preferredMode,
            reason,
            packageUrl ?? $"https://www.nuget.org/packages/{packageId}/{version}",
            packageContentUrl,
            catalogEntryUrl,
            PackageTitle: catalogLeaf.Title,
            PackageDescription: catalogLeaf.Description);
    }

    private static string? DetectCliFramework(CatalogLeaf catalogLeaf, SpectrePackageInspection? packageInspection)
    {
        if (HasConfirmedSpectreCli(catalogLeaf, packageInspection))
        {
            var classified = CliFrameworkProviderRegistry.Detect(catalogLeaf);
            return string.IsNullOrWhiteSpace(classified) || string.Equals(classified, "Spectre.Console.Cli", StringComparison.Ordinal)
                ? "Spectre.Console.Cli"
                : $"Spectre.Console.Cli + {classified}";
        }

        return CliFrameworkProviderRegistry.Detect(catalogLeaf);
    }

    private static (string PreferredMode, string Reason) SelectMode(CatalogLeaf catalogLeaf, SpectrePackageInspection? packageInspection, string? cliFramework)
        => HasConfirmedSpectreCli(catalogLeaf, packageInspection)
            ? ("native", "confirmed-spectre-console-cli")
            : CliFrameworkProviderRegistry.HasCliFxAnalysisSupport(cliFramework)
                ? ("clifx", "confirmed-clifx")
                : CliFrameworkProviderRegistry.HasStaticAnalysisSupport(cliFramework)
                    ? ("static", "confirmed-static-analysis-framework")
                    : ("help", "generic-help-crawl");

    private static bool HasConfirmedSpectreCli(CatalogLeaf catalogLeaf, SpectrePackageInspection? packageInspection)
    {
        var dependencyIds = (catalogLeaf.DependencyGroups ?? [])
            .SelectMany(group => group.Dependencies ?? [])
            .Select(dependency => dependency.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
        var packageEntryNames = (catalogLeaf.PackageEntries ?? [])
            .Select(entry => entry.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        return dependencyIds.Any(id => string.Equals(id, "Spectre.Console.Cli", StringComparison.OrdinalIgnoreCase))
            || packageInspection?.ToolAssembliesReferencingSpectreConsoleCli.Count > 0
            || packageInspection?.SpectreConsoleCliDependencyVersions.Count > 0
            || packageEntryNames.Any(name => string.Equals(name, "Spectre.Console.Cli.dll", StringComparison.OrdinalIgnoreCase));
    }
}

