namespace InSpectra.Discovery.Tool.NuGet;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

internal sealed class NuGetApiClient
{
    private readonly HttpClient _httpClient;

    public NuGetApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InSpectra.Discovery.Tool", "0.1.0"));
    }

    public Task<NuGetServiceIndex> GetServiceResourcesAsync(string serviceIndexUrl, CancellationToken cancellationToken)
        => GetJsonAsync<NuGetServiceIndexSpec, NuGetServiceIndex>(
            serviceIndexUrl,
            NuGetApiModelMapper.ToModel,
            cancellationToken);

    public Task<CatalogIndex> GetCatalogIndexAsync(string catalogIndexUrl, CancellationToken cancellationToken)
        => GetJsonAsync<CatalogIndexSpec, CatalogIndex>(
            catalogIndexUrl,
            NuGetApiModelMapper.ToModel,
            cancellationToken);

    public Task<CatalogPage> GetCatalogPageAsync(string pageUrl, CancellationToken cancellationToken)
        => GetJsonAsync<CatalogPageSpec, CatalogPage>(
            pageUrl,
            NuGetApiModelMapper.ToModel,
            cancellationToken);

    public Task<SearchResponse> SearchAsync(
        string searchUrl,
        string query,
        int skip,
        int take,
        string packageType,
        CancellationToken cancellationToken)
        => GetJsonAsync<SearchResponseSpec, SearchResponse>(
            $"{searchUrl}?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}&prerelease=true&semVerLevel=2.0.0&packageType={packageType}",
            NuGetApiModelMapper.ToModel,
            cancellationToken);

    public Task<AutocompleteResponse> AutocompleteAsync(
        string autocompleteUrl,
        string query,
        int skip,
        int take,
        string packageType,
        CancellationToken cancellationToken)
        => GetJsonAsync<AutocompleteResponseSpec, AutocompleteResponse>(
            $"{autocompleteUrl}?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}&prerelease=true&semVerLevel=2.0.0&packageType={packageType}",
            NuGetApiModelMapper.ToModel,
            cancellationToken);

    public Task<RegistrationIndex> GetRegistrationIndexAsync(
        string registrationBaseUrl,
        string packageId,
        CancellationToken cancellationToken)
        => GetRegistrationIndexByUrlAsync(
            $"{registrationBaseUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json",
            cancellationToken);

    public Task<RegistrationIndex> GetRegistrationIndexByUrlAsync(string registrationIndexUrl, CancellationToken cancellationToken)
        => GetJsonAsync<RegistrationIndexSpec, RegistrationIndex>(
            registrationIndexUrl,
            NuGetApiModelMapper.ToModel,
            cancellationToken);

    public Task<RegistrationPage> GetRegistrationPageAsync(string pageUrl, CancellationToken cancellationToken)
        => GetJsonAsync<RegistrationPageSpec, RegistrationPage>(
            pageUrl,
            NuGetApiModelMapper.ToModel,
            cancellationToken);

    public Task<RegistrationLeafDocument> GetRegistrationLeafAsync(string leafUrl, CancellationToken cancellationToken)
        => GetJsonAsync<RegistrationLeafDocumentSpec, RegistrationLeafDocument>(
            leafUrl,
            NuGetApiModelMapper.ToModel,
            cancellationToken);

    public Task<CatalogLeaf> GetCatalogLeafAsync(string catalogEntryUrl, CancellationToken cancellationToken)
        => GetJsonAsync<CatalogLeafSpec, CatalogLeaf>(
            catalogEntryUrl,
            NuGetApiModelMapper.ToModel,
            cancellationToken);

    public async Task<int> GetSearchTotalHitsAsync(string searchUrl, CancellationToken cancellationToken)
    {
        var response = await SearchAsync(searchUrl, string.Empty, skip: 0, take: 1, packageType: "dotnettool", cancellationToken);
        return response.TotalHits;
    }

    public async Task<long> GetPackageTotalDownloadsAsync(string searchUrl, string packageId, CancellationToken cancellationToken)
    {
        var totalDownloads = await TryGetPackageTotalDownloadsAsync(searchUrl, packageId, cancellationToken);
        return totalDownloads ?? throw new InvalidOperationException($"Could not resolve search metadata for '{packageId}'.");
    }

    public async Task<long?> TryGetPackageTotalDownloadsAsync(string searchUrl, string packageId, CancellationToken cancellationToken)
    {
        var queries = new[]
        {
            $"packageid:{packageId}",
            $"packageid:\"{packageId}\"",
            packageId,
        };

        foreach (var query in queries)
        {
            var response = await SearchAsync(searchUrl, query, skip: 0, take: 20, packageType: "dotnettool", cancellationToken);
            var match = response.Data.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, packageId, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match.TotalDownloads;
            }
        }

        return null;
    }

    public async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (attempt < 4 && IsRetryable(response.StatusCode))
            {
                await Task.Delay(delay, cancellationToken);
                delay = delay + delay;
                continue;
            }

            response.EnsureSuccessStatusCode();

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destinationStream = File.Create(destinationPath);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Exhausted retries for '{url}'.");
    }

    private async Task<TModel> GetJsonAsync<TSpec, TModel>(
        string url,
        Func<TSpec, TModel> map,
        CancellationToken cancellationToken)
        where TSpec : class
    {
        var delay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (attempt < 4 && IsRetryable(response.StatusCode))
            {
                await Task.Delay(delay, cancellationToken);
                delay = delay + delay;
                continue;
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            try
            {
                var spec = await JsonSerializer.DeserializeAsync<TSpec>(stream, cancellationToken: cancellationToken);
                if (spec is null)
                {
                    throw new JsonException("JSON payload was null.");
                }

                return map(spec);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize JSON from '{url}' as {typeof(TModel).Name}: {ex.Message}",
                    ex);
            }
        }

        throw new InvalidOperationException($"Exhausted retries for '{url}'.");
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}

