internal interface IToolAnalysisDescriptorResolver
{
    Task<ToolAnalysisDescriptor> ResolveAsync(string packageId, string version, CancellationToken cancellationToken);
}

internal sealed class ToolAnalysisDescriptorResolver : IToolAnalysisDescriptorResolver
{
    public async Task<ToolAnalysisDescriptor> ResolveAsync(string packageId, string version, CancellationToken cancellationToken)
    {
        using var scope = ToolRuntime.CreateNuGetApiClientScope();
        var (leaf, catalogLeaf) = await PackageVersionResolver.ResolveAsync(scope.Client, packageId, version, cancellationToken);
        var packageInspection = await new PackageArchiveInspector(scope.Client).InspectAsync(leaf.PackageContent, cancellationToken);
        var cliFramework = DetectCliFramework(catalogLeaf, packageInspection);
        var (preferredMode, reason) = SelectMode(catalogLeaf, packageInspection, cliFramework);

        return new ToolAnalysisDescriptor(
            packageId,
            version,
            packageInspection.ToolCommandNames.FirstOrDefault(),
            cliFramework,
            preferredMode,
            reason,
            $"https://www.nuget.org/packages/{packageId}/{version}",
            leaf.PackageContent,
            leaf.CatalogEntryUrl);
    }

    private static string? DetectCliFramework(CatalogLeaf catalogLeaf, SpectrePackageInspection packageInspection)
    {
        if (HasConfirmedSpectreCli(catalogLeaf, packageInspection))
        {
            var classified = CliFrameworkCatalogClassifier.Detect(catalogLeaf);
            return string.IsNullOrWhiteSpace(classified) || string.Equals(classified, "Spectre.Console.Cli", StringComparison.Ordinal)
                ? "Spectre.Console.Cli"
                : $"Spectre.Console.Cli + {classified}";
        }

        return CliFrameworkCatalogClassifier.Detect(catalogLeaf);
    }

    private static (string PreferredMode, string Reason) SelectMode(CatalogLeaf catalogLeaf, SpectrePackageInspection packageInspection, string? cliFramework)
        => HasConfirmedSpectreCli(catalogLeaf, packageInspection)
            ? ("native", "confirmed-spectre-console-cli")
            : CliFrameworkSupport.HasCliFx(cliFramework)
                ? ("clifx", "confirmed-clifx")
                : ("help", "generic-help-crawl");

    private static bool HasConfirmedSpectreCli(CatalogLeaf catalogLeaf, SpectrePackageInspection packageInspection)
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
            || packageInspection.ToolAssembliesReferencingSpectreConsoleCli.Count > 0
            || packageInspection.SpectreConsoleCliDependencyVersions.Count > 0
            || packageEntryNames.Any(name => string.Equals(name, "Spectre.Console.Cli.dll", StringComparison.OrdinalIgnoreCase));
    }
}
