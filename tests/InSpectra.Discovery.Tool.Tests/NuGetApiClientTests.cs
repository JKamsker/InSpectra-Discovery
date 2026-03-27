using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Xunit;

public sealed class NuGetApiClientTests
{
    private const string ServiceIndexUrl = "https://nuget.test/index.json";
    private const string RegistrationPageUrl = "https://nuget.test/registration/cute/page/2.14.0/2.15.0.json";
    private const string RegistrationLeafUrl = "https://nuget.test/registration/cute/2.15.0.json";
    private const string CatalogEntryUrl = "https://nuget.test/catalog/cute.2.15.0.json";
    private const string CatalogLeafUrl = "https://nuget.test/catalog/mstestx.console.0.37.0.json";
    private const string PackageContentUrl = "https://nuget.test/flat/cute.2.15.0.nupkg";
    private static readonly DateTimeOffset PublishedAt = DateTimeOffset.Parse("2026-03-27T13:24:43.773+01:00");

    [Fact]
    public async Task GetServiceResourcesAsync_ParsesTypeArraysThroughConverter()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ServiceIndexUrl] = """
                {
                  "resources": [
                    {
                      "@id": "https://nuget.test/catalog",
                      "@type": [
                        "Catalog/3.0.0",
                        "Catalog/3.0.0-rc"
                      ]
                    }
                  ]
                }
                """,
        }));
        var client = new NuGetApiClient(httpClient);

        var serviceIndex = await client.GetServiceResourcesAsync(ServiceIndexUrl, CancellationToken.None);

        Assert.Equal("https://nuget.test/catalog", serviceIndex.GetRequiredResource("Catalog/3.0.0"));
    }

    [Fact]
    public async Task GetRegistrationPageAsync_ParsesEmbeddedCatalogEntryPayload()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RegistrationPageUrl] = """
                {
                  "items": [
                    {
                      "@id": "https://nuget.test/registration/cute/2.15.0.json",
                      "commitTimeStamp": "2026-03-27T13:29:43.8550432+01:00",
                      "catalogEntry": {
                        "@id": "https://nuget.test/catalog/cute.2.15.0.json",
                        "authors": "Andre Sharpe",
                        "description": "Contentful Update Tool and Extractor.",
                        "listed": true,
                        "projectUrl": "https://github.com/andresharpe/cute",
                        "published": "2026-03-27T13:24:43.773+01:00",
                        "repository": {
                          "type": "git",
                          "url": "https://github.com/andresharpe/cute.git"
                        },
                        "readmeUrl": "https://www.nuget.org/packages/cute/2.15.0#show-readme-container",
                        "version": "2.15.0"
                      },
                      "packageContent": "https://nuget.test/flat/cute.2.15.0.nupkg"
                    }
                  ]
                }
                """,
        }));
        var client = new NuGetApiClient(httpClient);

        var page = await client.GetRegistrationPageAsync(RegistrationPageUrl, CancellationToken.None);

        var leaf = Assert.Single(page.Items);
        Assert.Equal(CatalogEntryUrl, leaf.CatalogEntry.Id);
        Assert.Equal("2.15.0", leaf.CatalogEntry.Version);
        Assert.True(leaf.CatalogEntry.Listed);
        Assert.Equal(PublishedAt.UtcDateTime, leaf.CatalogEntry.Published?.UtcDateTime);
        Assert.Equal("https://github.com/andresharpe/cute.git", leaf.CatalogEntry.Repository?.Url);
        Assert.Equal(PackageContentUrl, leaf.PackageContent);
    }

    [Fact]
    public async Task GetRegistrationLeafAsync_ParsesPermalinkPayload()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RegistrationLeafUrl] = """
                {
                  "@id": "https://nuget.test/registration/cute/2.15.0.json",
                  "catalogEntry": "https://nuget.test/catalog/cute.2.15.0.json",
                  "listed": true,
                  "packageContent": "https://nuget.test/flat/cute.2.15.0.nupkg",
                  "published": "2026-03-27T13:24:43.773+01:00"
                }
                """,
        }));
        var client = new NuGetApiClient(httpClient);

        var leaf = await client.GetRegistrationLeafAsync(RegistrationLeafUrl, CancellationToken.None);

        Assert.Equal(CatalogEntryUrl, leaf.CatalogEntryUrl);
        Assert.True(leaf.Listed);
        Assert.Equal(PackageContentUrl, leaf.PackageContent);
        Assert.Equal(PublishedAt.UtcDateTime, leaf.Published?.UtcDateTime);
    }

    [Fact]
    public async Task GetRegistrationLeafAsync_ReportsUrlAndTargetTypeOnShapeMismatch()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RegistrationLeafUrl] = """
                {
                  "catalogEntry": {
                    "@id": "https://nuget.test/catalog/cute.2.15.0.json"
                  },
                  "packageContent": "https://nuget.test/flat/cute.2.15.0.nupkg"
                }
                """,
        }));
        var client = new NuGetApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetRegistrationLeafAsync(RegistrationLeafUrl, CancellationToken.None));

        Assert.Contains(RegistrationLeafUrl, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(RegistrationLeafDocument), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCatalogLeafAsync_AllowsEmptyStringRepository()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CatalogLeafUrl] = """
                {
                  "@id": "https://nuget.test/catalog/mstestx.console.0.37.0.json",
                  "projectUrl": "https://github.com/dotMorten/MSTestX",
                  "repository": "",
                  "packageTypes": [
                    {
                      "name": "DotnetTool"
                    }
                  ]
                }
                """,
        }));
        var client = new NuGetApiClient(httpClient);

        var leaf = await client.GetCatalogLeafAsync(CatalogLeafUrl, CancellationToken.None);

        Assert.Equal(CatalogLeafUrl, leaf.Id);
        Assert.Equal("https://github.com/dotMorten/MSTestX", leaf.ProjectUrl);
        Assert.Null(leaf.Repository);
        Assert.True(leaf.PackageTypes.HasValue);
        var packageType = Assert.Single(leaf.PackageTypes.Value.EnumerateArray());
        Assert.Equal("DotnetTool", packageType.GetProperty("name").GetString());
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, string> _responses;

        public StubHttpMessageHandler(IReadOnlyDictionary<string, string> responses)
        {
            _responses = responses;
        }

        public ConcurrentQueue<string> RequestedUris { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? throw new InvalidOperationException("Request URI is missing.");
            RequestedUris.Enqueue(uri);

            if (!_responses.TryGetValue(uri, out var content))
            {
                throw new InvalidOperationException($"Unexpected request URI '{uri}'.");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            });
        }
    }
}
