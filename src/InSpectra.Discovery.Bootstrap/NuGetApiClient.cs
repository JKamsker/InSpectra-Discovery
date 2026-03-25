using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

internal sealed class NuGetApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;

    public NuGetApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InSpectra.Discovery.Bootstrap", "0.1.0"));
    }

    public Task<NuGetServiceIndex> GetServiceResourcesAsync(string serviceIndexUrl, CancellationToken cancellationToken)
        => GetJsonAsync<NuGetServiceIndex>(serviceIndexUrl, cancellationToken);

    public Task<CatalogIndex> GetCatalogIndexAsync(string catalogIndexUrl, CancellationToken cancellationToken)
        => GetJsonAsync<CatalogIndex>(catalogIndexUrl, cancellationToken);

    public Task<CatalogPage> GetCatalogPageAsync(string pageUrl, CancellationToken cancellationToken)
        => GetJsonAsync<CatalogPage>(pageUrl, cancellationToken);

    public Task<SearchResponse> SearchAsync(
        string searchUrl,
        string query,
        int skip,
        int take,
        string packageType,
        CancellationToken cancellationToken)
        => GetJsonAsync<SearchResponse>(
            $"{searchUrl}?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}&prerelease=true&semVerLevel=2.0.0&packageType={packageType}",
            cancellationToken);

    public Task<AutocompleteResponse> AutocompleteAsync(
        string autocompleteUrl,
        string query,
        int skip,
        int take,
        string packageType,
        CancellationToken cancellationToken)
        => GetJsonAsync<AutocompleteResponse>(
            $"{autocompleteUrl}?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}&prerelease=true&semVerLevel=2.0.0&packageType={packageType}",
            cancellationToken);

    public Task<RegistrationIndex> GetRegistrationIndexAsync(
        string registrationBaseUrl,
        string packageId,
        CancellationToken cancellationToken)
        => GetJsonAsync<RegistrationIndex>(
            $"{registrationBaseUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json",
            cancellationToken);

    public Task<RegistrationPage> GetRegistrationPageAsync(string pageUrl, CancellationToken cancellationToken)
        => GetJsonAsync<RegistrationPage>(pageUrl, cancellationToken);

    public Task<CatalogLeaf> GetCatalogLeafAsync(string catalogEntryUrl, CancellationToken cancellationToken)
        => GetJsonAsync<CatalogLeaf>(catalogEntryUrl, cancellationToken);

    public async Task<int> GetSearchTotalHitsAsync(string searchUrl, CancellationToken cancellationToken)
    {
        var response = await SearchAsync(searchUrl, string.Empty, skip: 0, take: 1, packageType: "dotnettool", cancellationToken);
        return response.TotalHits;
    }

    public async Task<long> GetPackageTotalDownloadsAsync(string searchUrl, string packageId, CancellationToken cancellationToken)
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

        throw new InvalidOperationException($"Could not resolve search metadata for '{packageId}'.");
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

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
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

            var value = await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
            return value ?? throw new InvalidOperationException($"Received an empty JSON payload from '{url}'.");
        }

        throw new InvalidOperationException($"Exhausted retries for '{url}'.");
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
