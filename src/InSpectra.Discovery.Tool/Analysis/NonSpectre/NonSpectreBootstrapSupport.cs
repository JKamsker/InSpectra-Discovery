namespace InSpectra.Discovery.Tool.Analysis.NonSpectre;

using InSpectra.Discovery.Tool.Packages;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.NuGet;

using System.Text.Json.Nodes;

internal static class NonSpectreBootstrapSupport
{
    public static async Task<NonSpectreAnalysisBootstrapResult> PopulateResultAsync(
        JsonObject result,
        NuGetApiClient apiClient,
        string packageId,
        string version,
        string? commandName,
        CancellationToken cancellationToken)
    {
        var (registrationLeaf, catalogLeaf) = await PackageVersionResolver.ResolveAsync(apiClient, packageId, version, cancellationToken);
        ApplyPackageMetadata(result, packageId, version, registrationLeaf, catalogLeaf);

        return new NonSpectreAnalysisBootstrapResult(
            registrationLeaf.PackageContent,
            await ResolveCommandInfoAsync(apiClient, registrationLeaf.PackageContent, commandName, cancellationToken));
    }

    private static void ApplyPackageMetadata(
        JsonObject result,
        string packageId,
        string version,
        RegistrationLeafDocument registrationLeaf,
        CatalogLeaf catalogLeaf)
    {
        result["packageUrl"] = $"https://www.nuget.org/packages/{packageId}/{version}";
        result["projectUrl"] = catalogLeaf.ProjectUrl;
        result["sourceRepositoryUrl"] = PackageVersionResolver.NormalizeRepositoryUrl(catalogLeaf.Repository?.Url);
        result["registrationLeafUrl"] = registrationLeaf.Id;
        result["catalogEntryUrl"] = registrationLeaf.CatalogEntryUrl;
        result["packageContentUrl"] = registrationLeaf.PackageContent;
        result["publishedAt"] = registrationLeaf.Published?.ToUniversalTime().ToString("O");
        result["nugetTitle"] = catalogLeaf.Title;
        result["nugetDescription"] = catalogLeaf.Description;
    }

    private static async Task<ResolvedToolCommandInfo> ResolveCommandInfoAsync(
        NuGetApiClient apiClient,
        string packageContentUrl,
        string? commandName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(commandName))
        {
            return new ResolvedToolCommandInfo(commandName, null, null);
        }

        var packageLayout = await new PackageToolCommandInspector(apiClient).InspectAsync(packageContentUrl, cancellationToken);
        return new ResolvedToolCommandInfo(
            packageLayout.ToolCommandNames.FirstOrDefault(),
            packageLayout.ToolEntryPointPaths.FirstOrDefault(),
            packageLayout.ToolSettingsPaths.FirstOrDefault());
    }
}


