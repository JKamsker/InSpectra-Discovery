using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

public sealed class DotnetToolCatalogDeltaDiscovererTests
{
    private const string PackageId = "Muonroi.BuildingBlock";
    private const string ServiceIndexUrl = "https://nuget.test/v3/index.json";
    private const string CatalogIndexUrl = "https://nuget.test/catalog/index.json";
    private const string CatalogPageUrl = "https://nuget.test/catalog/page-1.json";
    private const string SearchUrl = "https://nuget.test/search";
    private const string AutocompleteUrl = "https://nuget.test/autocomplete";
    private const string RegistrationBaseUrl = "https://nuget.test/registration";
    private const string RegistrationIndexUrl = "https://nuget.test/registration/muonroi.buildingblock/index.json";
    private const string HistoricalToolLeafUrl = "https://nuget.test/catalog/muonroi.buildingblock.1.0.0.json";
    private const string RepublishedLatestLeafUrl = "https://nuget.test/catalog/muonroi.buildingblock.1.9.3.json";
    private const string LatestListedLeafUrl = "https://nuget.test/catalog/muonroi.buildingblock.1.9.2.json";
    private static readonly DateTimeOffset SnapshotGeneratedAtUtc = DateTimeOffset.Parse("2026-03-26T08:00:00Z");
    private static readonly DateTimeOffset CatalogCommitTimeUtc = DateTimeOffset.Parse("2026-03-26T08:21:15Z");
    private static readonly DateTimeOffset ListedPublishedAtUtc = DateTimeOffset.Parse("2026-02-07T10:46:09Z");

    [Fact]
    public async Task RunAsync_IgnoresRepublishedHistoricalTool_WhenLatestListedVersionIsNotADotnetTool()
    {
        using var tempDirectory = new TemporaryDirectory();
        var currentSnapshotPath = tempDirectory.GetPath("dotnet-tools.current.json");
        await WriteJsonAsync(currentSnapshotPath, CreateSnapshot(Array.Empty<DotnetToolIndexEntry>()));

        var handler = new StubHttpMessageHandler(CreateResponses(HistoricalToolLeafUrl, CatalogLeafJson(isDotnetTool: true)));
        using var httpClient = new HttpClient(handler);
        var apiClient = new NuGetApiClient(httpClient);
        var discoverer = new DotnetToolCatalogDeltaDiscoverer(apiClient);

        var computation = await discoverer.RunAsync(
            CreateOptions(currentSnapshotPath, tempDirectory.GetPath("dotnet-tools.cursor.json")),
            reportProgress: null,
            CancellationToken.None);

        Assert.Equal(1, computation.Delta.AffectedPackageCount);
        Assert.Equal(0, computation.Delta.ChangedPackageCount);
        Assert.Empty(computation.Delta.Packages);
        Assert.Empty(computation.UpdatedCurrentSnapshot.Packages);
        Assert.Contains(LatestListedLeafUrl, handler.RequestedUris);
        Assert.DoesNotContain(handler.RequestedUris, uri => uri.StartsWith(SearchUrl, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_RemovesSnapshotEntry_WhenLatestListedVersionIsNotADotnetTool()
    {
        using var tempDirectory = new TemporaryDirectory();
        var currentSnapshotPath = tempDirectory.GetPath("dotnet-tools.current.json");
        await WriteJsonAsync(currentSnapshotPath, CreateSnapshot(new[] { CreateSnapshotEntry() }));

        var handler = new StubHttpMessageHandler(CreateResponses(RepublishedLatestLeafUrl, CatalogLeafJson(isDotnetTool: false)));
        using var httpClient = new HttpClient(handler);
        var apiClient = new NuGetApiClient(httpClient);
        var discoverer = new DotnetToolCatalogDeltaDiscoverer(apiClient);

        var computation = await discoverer.RunAsync(
            CreateOptions(currentSnapshotPath, tempDirectory.GetPath("dotnet-tools.cursor.json")),
            reportProgress: null,
            CancellationToken.None);

        var change = Assert.Single(computation.Delta.Packages);
        Assert.Equal(1, computation.Delta.AffectedPackageCount);
        Assert.Equal(1, computation.Delta.ChangedPackageCount);
        Assert.Equal(PackageId, change.PackageId);
        Assert.Equal("removed", change.ChangeKind);
        Assert.Equal("1.0.0", change.PreviousVersion);
        Assert.Null(change.CurrentVersion);
        Assert.NotNull(change.Previous);
        Assert.Null(change.Current);
        Assert.Empty(computation.UpdatedCurrentSnapshot.Packages);
        Assert.Equal(0, computation.UpdatedCurrentSnapshot.PackageCount);
        Assert.DoesNotContain(handler.RequestedUris, uri => uri.StartsWith(SearchUrl, StringComparison.Ordinal));
    }

    private static IndexDeltaOptions CreateOptions(string currentSnapshotPath, string cursorStatePath)
        => new()
        {
            CurrentSnapshotPath = currentSnapshotPath,
            CursorStatePath = cursorStatePath,
            ServiceIndexUrl = ServiceIndexUrl,
            Concurrency = 1,
            OverlapMinutes = 0,
        };

    private static DotnetToolIndexSnapshot CreateSnapshot(IReadOnlyList<DotnetToolIndexEntry> packages)
        => new(
            GeneratedAtUtc: SnapshotGeneratedAtUtc,
            PackageType: "DotnetTool",
            PackageCount: packages.Count,
            Source: new DotnetToolIndexSource(
                ServiceIndexUrl,
                AutocompleteUrl,
                SearchUrl,
                RegistrationBaseUrl,
                "abcdefghijklmnopqrstuvwxyz",
                packages.Count,
                "totalDownloads-desc"),
            Packages: packages);

    private static DotnetToolIndexEntry CreateSnapshotEntry()
        => new(
            PackageId: PackageId,
            LatestVersion: "1.0.0",
            TotalDownloads: 42,
            VersionCount: 1,
            Listed: true,
            PublishedAtUtc: DateTimeOffset.Parse("2024-08-25T06:10:21Z"),
            CommitTimestampUtc: DateTimeOffset.Parse("2026-03-26T08:21:15Z"),
            ProjectUrl: null,
            PackageUrl: "https://www.nuget.org/packages/Muonroi.BuildingBlock/1.0.0",
            PackageContentUrl: "https://nuget.test/flat/muonroi.buildingblock.1.0.0.nupkg",
            RegistrationUrl: RegistrationIndexUrl,
            CatalogEntryUrl: HistoricalToolLeafUrl,
            Authors: "Muonroi.BuildingBlock",
            Description: "Republished historical dotnet tool.",
            LicenseExpression: null,
            LicenseUrl: null,
            ReadmeUrl: null);

    private static IReadOnlyDictionary<string, string> CreateResponses(string pageLeafUrl, string pageLeafJson)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ServiceIndexUrl] = ServiceIndexJson(),
            [CatalogIndexUrl] = CatalogIndexJson(),
            [CatalogPageUrl] = CatalogPageJson(pageLeafUrl),
            [pageLeafUrl] = pageLeafJson,
            [RegistrationIndexUrl] = RegistrationIndexJson(),
            [LatestListedLeafUrl] = CatalogLeafJson(isDotnetTool: false),
        };

    private static string ServiceIndexJson() => $$"""
        {
          "resources": [
            { "@id": "{{CatalogIndexUrl}}", "@type": "Catalog/3.0.0" },
            { "@id": "{{SearchUrl}}", "@type": "SearchQueryService/3.5.0" },
            { "@id": "{{AutocompleteUrl}}", "@type": "SearchAutocompleteService/3.5.0" },
            { "@id": "{{RegistrationBaseUrl}}/", "@type": "RegistrationsBaseUrl/Versioned" }
          ]
        }
        """;

    private static string CatalogIndexJson() => $$"""
        {
          "items": [
            { "@id": "{{CatalogPageUrl}}", "commitTimeStamp": "{{CatalogCommitTimeUtc:O}}" }
          ]
        }
        """;

    private static string CatalogPageJson(string pageLeafUrl) => $$"""
        {
          "items": [
            {
              "@id": "{{pageLeafUrl}}",
              "@type": "nuget:PackageDetails",
              "commitTimeStamp": "{{CatalogCommitTimeUtc:O}}",
              "nuget:id": "{{PackageId}}",
              "nuget:version": "1.0.0"
            }
          ]
        }
        """;

    private static string RegistrationIndexJson() => $$"""
        {
          "@id": "{{RegistrationIndexUrl}}",
          "items": [
            {
              "@id": "{{RegistrationIndexUrl}}#page/1",
              "count": 1,
              "items": [
                {
                  "commitTimeStamp": "{{CatalogCommitTimeUtc:O}}",
                  "catalogEntry": {
                    "@id": "{{LatestListedLeafUrl}}",
                    "listed": true,
                    "published": "{{ListedPublishedAtUtc:O}}",
                    "version": "1.9.2"
                  },
                  "packageContent": "https://nuget.test/flat/muonroi.buildingblock.1.9.2.nupkg"
                }
              ]
            }
          ]
        }
        """;

    private static string CatalogLeafJson(bool isDotnetTool)
        => isDotnetTool
            ? $$"""
                {
                  "@id": "{{HistoricalToolLeafUrl}}",
                  "packageTypes": [
                    { "name": "DotnetTool" }
                  ]
                }
                """
            : $$"""
                {
                  "@id": "{{LatestListedLeafUrl}}"
                }
                """;

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions.Default);
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"inspectra-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string GetPath(string fileName) => System.IO.Path.Combine(Path, fileName);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
