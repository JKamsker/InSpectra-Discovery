namespace InSpectra.Discovery.Tool.Tests;

using System.Net;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Xunit.Abstractions;

public sealed class NuGetApiClientLiveTests
{
    private const string EnableEnvVar = "INSPECTRA_DISCOVERY_LIVE_NUGET_TESTS";
    private const string ScopeEnvVar = "INSPECTRA_DISCOVERY_LIVE_NUGET_SCOPE";
    private const string ServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    private readonly ITestOutputHelper _output;

    public NuGetApiClientLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task Live_ServiceIndexAndCatalogPages_Parse()
    {
        if (!ShouldRun())
        {
            return;
        }

        using var httpClient = CreateHttpClient();
        var client = new NuGetApiClient(httpClient);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        var resources = await client.GetServiceResourcesAsync(ServiceIndexUrl, cts.Token);
        var catalogIndexUrl = resources.GetRequiredResource("Catalog/3.0.0");
        var catalogIndex = await client.GetCatalogIndexAsync(catalogIndexUrl, cts.Token);

        Assert.NotEmpty(catalogIndex.Items);

        var latestPageReference = catalogIndex.Items
            .OrderByDescending(item => item.CommitTimeStamp)
            .First();

        var catalogPage = await client.GetCatalogPageAsync(latestPageReference.Id, cts.Token);
        Assert.NotEmpty(catalogPage.Items);
        Assert.All(catalogPage.Items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Id));
            Assert.False(string.IsNullOrWhiteSpace(item.Type));
            Assert.False(string.IsNullOrWhiteSpace(item.PackageId));
            Assert.False(string.IsNullOrWhiteSpace(item.PackageVersion));
        });
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task Live_IndexedPackageRegistrationAndCatalogPayloads_Parse()
    {
        if (!ShouldRun())
        {
            return;
        }

        var metadataEntries = LoadIndexedPackageMetadata().ToArray();
        _output.WriteLine($"Validating registration/catalog payloads for {metadataEntries.Length} indexed package entries.");

        using var httpClient = CreateHttpClient();
        var client = new NuGetApiClient(httpClient);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));

        var resources = await client.GetServiceResourcesAsync(ServiceIndexUrl, cts.Token);
        var registrationBaseUrl = resources.GetRequiredResource("RegistrationsBaseUrl/Versioned", "RegistrationsBaseUrl/3.6.0");

        var failures = new ConcurrentQueue<string>();
        var completed = 0;

        await Parallel.ForEachAsync(
            metadataEntries,
            new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = 12,
            },
            async (entry, token) =>
            {
                try
                {
                    var registrationIndex = await client.GetRegistrationIndexAsync(registrationBaseUrl, entry.PackageId, token);
                    var registrationPageLeaf = await FindLeafAsync(client, registrationIndex, entry.Version, token)
                        ?? throw new InvalidOperationException($"Could not find version '{entry.Version}' in registration index.");

                    var registrationLeaf = await client.GetRegistrationLeafAsync(entry.RegistrationLeafUrl, token);
                    var catalogLeaf = await client.GetCatalogLeafAsync(entry.CatalogEntryUrl, token);

                    Assert.Equal(entry.Version, registrationPageLeaf.CatalogEntry.Version, ignoreCase: true);
                    Assert.Equal(entry.CatalogEntryUrl, registrationLeaf.CatalogEntryUrl, ignoreCase: true);
                    Assert.Equal(entry.CatalogEntryUrl, catalogLeaf.Id, ignoreCase: true);
                    Assert.Equal(registrationPageLeaf.PackageContent, registrationLeaf.PackageContent, ignoreCase: true);
                }
                catch (Exception ex)
                {
                    failures.Enqueue($"{entry.PackageId} {entry.Version}: {ex.Message}");
                }
                finally
                {
                    var current = Interlocked.Increment(ref completed);
                    if (current % 25 == 0 || current == metadataEntries.Length)
                    {
                        _output.WriteLine($"Registration/catalog progress: {current}/{metadataEntries.Length}");
                    }
                }
            });

        AssertFailures(failures);
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task Live_IndexedPackageSearchAndAutocompletePayloads_Parse()
    {
        if (!ShouldRun())
        {
            return;
        }

        var metadataEntries = LoadIndexedPackageMetadata()
            .GroupBy(entry => entry.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        _output.WriteLine($"Validating search/autocomplete payloads for {metadataEntries.Length} indexed packages.");

        using var httpClient = CreateHttpClient();
        var client = new NuGetApiClient(httpClient);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));

        var resources = await client.GetServiceResourcesAsync(ServiceIndexUrl, cts.Token);
        var searchUrl = resources.GetRequiredResource("SearchQueryService/3.5.0");
        var autocompleteUrl = resources.GetRequiredResource("SearchAutocompleteService/3.5.0");

        var failures = new ConcurrentQueue<string>();
        var completed = 0;

        await Parallel.ForEachAsync(
            metadataEntries,
            new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = 8,
            },
            async (entry, token) =>
            {
                try
                {
                    var search = await client.SearchAsync(
                        searchUrl,
                        $"packageid:\"{entry.PackageId}\"",
                        skip: 0,
                        take: 20,
                        packageType: "dotnettool",
                        token);

                    if (!search.Data.Any(item => string.Equals(item.Id, entry.PackageId, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException("SearchQueryService did not return an exact package-id match.");
                    }

                    var autocomplete = await client.AutocompleteAsync(
                        autocompleteUrl,
                        entry.PackageId.ToLowerInvariant(),
                        skip: 0,
                        take: 20,
                        packageType: "dotnettool",
                        token);

                    if (!autocomplete.Data.Any(item => string.Equals(item, entry.PackageId, StringComparison.OrdinalIgnoreCase)))
                    {
                        var fallbackQuery = entry.PackageId[..Math.Min(entry.PackageId.Length, 4)].ToLowerInvariant();
                        autocomplete = await client.AutocompleteAsync(
                            autocompleteUrl,
                            fallbackQuery,
                            skip: 0,
                            take: 50,
                            packageType: "dotnettool",
                            token);
                    }

                    if (!autocomplete.Data.Any(item => string.Equals(item, entry.PackageId, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException("SearchAutocompleteService did not return an exact package-id match.");
                    }
                }
                catch (Exception ex)
                {
                    failures.Enqueue($"{entry.PackageId}: {ex.Message}");
                }
                finally
                {
                    var current = Interlocked.Increment(ref completed);
                    if (current % 25 == 0 || current == metadataEntries.Length)
                    {
                        _output.WriteLine($"Search/autocomplete progress: {current}/{metadataEntries.Length}");
                    }
                }
            });

        AssertFailures(failures);
    }

    private static HttpClient CreateHttpClient()
        => new(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };

    private static bool ShouldRun()
        => string.Equals(Environment.GetEnvironmentVariable(EnableEnvVar), "1", StringComparison.Ordinal);

    private static IEnumerable<IndexedPackageMetadata> LoadIndexedPackageMetadata()
    {
        var repositoryRoot = RepositoryPathResolver.ResolveRepositoryRoot();
        var packageRoot = Path.Combine(repositoryRoot, "index", "packages");

        var metadataFiles = string.Equals(Environment.GetEnvironmentVariable(ScopeEnvVar), "all", StringComparison.OrdinalIgnoreCase)
            ? Directory.EnumerateFiles(packageRoot, "metadata.json", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}latest{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            : Directory.EnumerateFiles(packageRoot, "metadata.json", SearchOption.AllDirectories)
                .Where(path => path.Contains($"{Path.DirectorySeparatorChar}latest{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var metadataPath in metadataFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var payload = JsonSerializer.Deserialize<IndexedPackageMetadata>(
                File.ReadAllText(metadataPath),
                JsonOptions.Default);

            if (payload is null)
            {
                throw new InvalidOperationException($"Failed to deserialize indexed metadata '{metadataPath}'.");
            }

            yield return payload;
        }
    }

    private static async Task<RegistrationPageLeaf?> FindLeafAsync(
        NuGetApiClient client,
        RegistrationIndex index,
        string version,
        CancellationToken cancellationToken)
    {
        foreach (var pageReference in index.Items)
        {
            var items = pageReference.Items
                ?? (await client.GetRegistrationPageAsync(pageReference.Id, cancellationToken)).Items;

            var match = items.FirstOrDefault(item =>
                string.Equals(item.CatalogEntry.Version, version, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static void AssertFailures(ConcurrentQueue<string> failures)
    {
        if (failures.IsEmpty)
        {
            return;
        }

        var sample = failures.Take(20).ToArray();
        var message = string.Join(Environment.NewLine, sample);
        if (failures.Count > sample.Length)
        {
            message += Environment.NewLine + $"... plus {failures.Count - sample.Length} more failures.";
        }

        Assert.Fail(message);
    }

    private sealed record IndexedPackageMetadata(
        [property: JsonPropertyName("packageId")] string PackageId,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("registrationLeafUrl")] string RegistrationLeafUrl,
        [property: JsonPropertyName("catalogEntryUrl")] string CatalogEntryUrl);
}

