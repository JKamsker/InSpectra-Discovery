namespace InSpectra.Discovery.Tool.Packages;

using InSpectra.Discovery.Tool.NuGet;

internal sealed class PackageToolCommandInspector
{
    private readonly NuGetApiClient _apiClient;

    public PackageToolCommandInspector(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<DotnetToolPackageLayout> InspectAsync(string packageContentUrl, CancellationToken cancellationToken)
        => PackageArchiveInspectionSupport.InspectAsync(
            _apiClient,
            packageContentUrl,
            "inspectra-tool-command",
            DotnetToolPackageLayoutReader.Read,
            cancellationToken);
}

