namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Json;
using InSpectra.Discovery.Tool.NuGet;

using InSpectra.Discovery.Tool.Catalog.Delta;
using InSpectra.Discovery.Tool.Catalog.Indexing;

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

public sealed class DotnetToolCatalogDeltaDiscovererNonFatalTests
{
    private const string ServiceIndexUrl = "https://nuget.test/v3/index.json";
    private const string CatalogIndexUrl = "https://nuget.test/catalog/index.json";
    private const string CatalogPageUrl = "https://nuget.test/catalog/page-1.json";
    private const string SearchUrl = "https://nuget.test/search";
    private const string AutocompleteUrl = "https://nuget.test/autocomplete";
    private const string RegistrationBaseUrl = "https://nuget.test/registration";
    private static readonly DateTimeOffset SnapshotGeneratedAtUtc = DateTimeOffset.Parse("2026-03-26T08:00:00Z");
    private static readonly DateTimeOffset CatalogCommitTimeUtc = DateTimeOffset.Parse("2026-03-26T08:21:15Z");
    private static readonly DateTimeOffset PublishedAtUtc = DateTimeOffset.Parse("2026-03-26T08:10:00Z");

    [Fact]
    public async Task RunAsync_SkipsSearchMetadataFailures_WithoutRemovingPreviousEntry_AndContinuesProcessingOtherPackages()
    {
        const string badPackageId = "Broken.Tool";
        const string goodPackageId = "Contoso.Tool";
        const string badDiscoveryLeafUrl = "https://nuget.test/catalog/broken.tool.discovery.json";
        const string badLatestLeafUrl = "https://nuget.test/catalog/broken.tool.1.1.0.json";
        const string goodDiscoveryLeafUrl = "https://nuget.test/catalog/contoso.tool.discovery.json";
        const string goodLatestLeafUrl = "https://nuget.test/catalog/contoso.tool.2.0.0.json";

        using var tempDirectory = new TemporaryDirectory();
        var currentSnapshotPath = tempDirectory.GetPath("dotnet-tools.current.json");
        await WriteJsonAsync(
            currentSnapshotPath,
            CreateSnapshot(new[]
            {
                CreateSnapshotEntry(
                    badPackageId,
                    latestVersion: "1.0.0",
                    totalDownloads: 42,
                    catalogEntryUrl: "https://nuget.test/catalog/broken.tool.1.0.0.json",
                    packageUrl: "https://www.nuget.org/packages/Broken.Tool/1.0.0",
                    packageContentUrl: "https://nuget.test/flat/broken.tool.1.0.0.nupkg",
                    registrationUrl: RegistrationIndexUrl(badPackageId)),
            }));

        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ServiceIndexUrl] = ServiceIndexJson(),
            [CatalogIndexUrl] = CatalogIndexJson(),
            [CatalogPageUrl] = CatalogPageJson(
                (badDiscoveryLeafUrl, badPackageId, "1.1.0"),
                (goodDiscoveryLeafUrl, goodPackageId, "2.0.0")),
            [goodDiscoveryLeafUrl] = CatalogLeafJson(isDotnetTool: true),
            [RegistrationIndexUrl(badPackageId)] = RegistrationIndexJson(badPackageId, "1.1.0", badLatestLeafUrl),
            [RegistrationIndexUrl(goodPackageId)] = RegistrationIndexJson(goodPackageId, "2.0.0", goodLatestLeafUrl),
            [badLatestLeafUrl] = CatalogLeafJson(isDotnetTool: true),
            [goodLatestLeafUrl] = CatalogLeafJson(isDotnetTool: true),
            [SearchQueryUrl($"packageid:{badPackageId}")] = SearchEmptyJson(),
            [SearchQueryUrl($"packageid:\"{badPackageId}\"")] = SearchEmptyJson(),
            [SearchQueryUrl(badPackageId)] = SearchEmptyJson(),
            [SearchQueryUrl($"packageid:{goodPackageId}")] = SearchResponseJson(goodPackageId, totalDownloads: 512),
        };

        var handler = new StubHttpMessageHandler(responses);
        using var httpClient = new HttpClient(handler);
        var discoverer = new DotnetToolCatalogDeltaDiscoverer(new NuGetApiClient(httpClient));

        var computation = await discoverer.RunAsync(
            CreateOptions(currentSnapshotPath, tempDirectory.GetPath("dotnet-tools.cursor.json")),
            reportProgress: null,
            CancellationToken.None);

        var change = Assert.Single(computation.Delta.Packages);
        Assert.Equal(2, computation.Delta.AffectedPackageCount);
        Assert.Equal(1, computation.Delta.ChangedPackageCount);
        Assert.Equal(goodPackageId, change.PackageId);
        Assert.Equal("added", change.ChangeKind);
        Assert.Equal("2.0.0", change.CurrentVersion);

        Assert.Equal(2, computation.UpdatedCurrentSnapshot.PackageCount);
        var updatedPackages = computation.UpdatedCurrentSnapshot.Packages.ToDictionary(entry => entry.PackageId, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("1.0.0", updatedPackages[badPackageId].LatestVersion);
        Assert.Equal("2.0.0", updatedPackages[goodPackageId].LatestVersion);
        Assert.Contains(handler.RequestedUris, uri => uri == SearchQueryUrl($"packageid:{badPackageId}"));
    }

    [Fact]
    public async Task RunAsync_SkipsCatalogLeafFailures_DuringAffectedPackageDetection()
    {
        const string brokenPackageId = "Exploding.Tool";
        const string goodPackageId = "Healthy.Tool";
        const string brokenDiscoveryLeafUrl = "https://nuget.test/catalog/exploding.tool.discovery.json";
        const string goodDiscoveryLeafUrl = "https://nuget.test/catalog/healthy.tool.discovery.json";
        const string goodLatestLeafUrl = "https://nuget.test/catalog/healthy.tool.1.2.3.json";

        using var tempDirectory = new TemporaryDirectory();
        var currentSnapshotPath = tempDirectory.GetPath("dotnet-tools.current.json");
        await WriteJsonAsync(currentSnapshotPath, CreateSnapshot(Array.Empty<DotnetToolIndexEntry>()));

        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ServiceIndexUrl] = ServiceIndexJson(),
            [CatalogIndexUrl] = CatalogIndexJson(),
            [CatalogPageUrl] = CatalogPageJson(
                (brokenDiscoveryLeafUrl, brokenPackageId, "9.9.9"),
                (goodDiscoveryLeafUrl, goodPackageId, "1.2.3")),
            [goodDiscoveryLeafUrl] = CatalogLeafJson(isDotnetTool: true),
            [RegistrationIndexUrl(goodPackageId)] = RegistrationIndexJson(goodPackageId, "1.2.3", goodLatestLeafUrl),
            [goodLatestLeafUrl] = CatalogLeafJson(isDotnetTool: true),
            [SearchQueryUrl($"packageid:{goodPackageId}")] = SearchResponseJson(goodPackageId, totalDownloads: 21),
        };

        var handler = new StubHttpMessageHandler(responses);
        using var httpClient = new HttpClient(handler);
        var discoverer = new DotnetToolCatalogDeltaDiscoverer(new NuGetApiClient(httpClient));

        var computation = await discoverer.RunAsync(
            CreateOptions(currentSnapshotPath, tempDirectory.GetPath("dotnet-tools.cursor.json")),
            reportProgress: null,
            CancellationToken.None);

        var change = Assert.Single(computation.Delta.Packages);
        Assert.Equal(1, computation.Delta.AffectedPackageCount);
        Assert.Equal(1, computation.Delta.ChangedPackageCount);
        Assert.Equal(goodPackageId, change.PackageId);
        Assert.Equal("added", change.ChangeKind);
        Assert.Equal(1, computation.UpdatedCurrentSnapshot.PackageCount);
        Assert.DoesNotContain(computation.UpdatedCurrentSnapshot.Packages, entry =>
            string.Equals(entry.PackageId, brokenPackageId, StringComparison.OrdinalIgnoreCase));
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

    private static DotnetToolIndexEntry CreateSnapshotEntry(
        string packageId,
        string latestVersion,
        long totalDownloads,
        string catalogEntryUrl,
        string packageUrl,
        string packageContentUrl,
        string registrationUrl)
        => new(
            PackageId: packageId,
            LatestVersion: latestVersion,
            TotalDownloads: totalDownloads,
            VersionCount: 1,
            Listed: true,
            PublishedAtUtc: PublishedAtUtc,
            CommitTimestampUtc: CatalogCommitTimeUtc,
            ProjectUrl: $"https://github.com/example/{packageId}",
            PackageUrl: packageUrl,
            PackageContentUrl: packageContentUrl,
            RegistrationUrl: registrationUrl,
            CatalogEntryUrl: catalogEntryUrl,
            Authors: packageId,
            Description: $"{packageId} description.",
            LicenseExpression: null,
            LicenseUrl: null,
            ReadmeUrl: null);

    private static string RegistrationIndexUrl(string packageId)
        => $"{RegistrationBaseUrl}/{packageId.ToLowerInvariant()}/index.json";

    private static string SearchQueryUrl(string query)
        => $"{SearchUrl}?q={Uri.EscapeDataString(query)}&skip=0&take=20&prerelease=true&semVerLevel=2.0.0&packageType=dotnettool";

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

    private static string CatalogPageJson(params (string leafUrl, string packageId, string version)[] items)
    {
        var pageItems = string.Join(
            ",",
            items.Select(item => $$"""
                {
                  "@id": "{{item.leafUrl}}",
                  "@type": "nuget:PackageDetails",
                  "commitTimeStamp": "{{CatalogCommitTimeUtc:O}}",
                  "nuget:id": "{{item.packageId}}",
                  "nuget:version": "{{item.version}}"
                }
                """));

        return $$"""
            {
              "items": [
                {{pageItems}}
              ]
            }
            """;
    }

    private static string RegistrationIndexJson(string packageId, string version, string latestLeafUrl) => $$"""
        {
          "@id": "{{RegistrationIndexUrl(packageId)}}",
          "items": [
            {
              "@id": "{{RegistrationIndexUrl(packageId)}}#page/1",
              "count": 1,
              "items": [
                {
                  "commitTimeStamp": "{{CatalogCommitTimeUtc:O}}",
                  "catalogEntry": {
                    "@id": "{{latestLeafUrl}}",
                    "authors": "{{packageId}}",
                    "description": "{{packageId}} description.",
                    "listed": true,
                    "projectUrl": "https://github.com/example/{{packageId}}",
                    "published": "{{PublishedAtUtc:O}}",
                    "version": "{{version}}"
                  },
                  "packageContent": "https://nuget.test/flat/{{packageId.ToLowerInvariant()}}.{{version}}.nupkg"
                }
              ]
            }
          ]
        }
        """;

    private static string CatalogLeafJson(bool isDotnetTool)
        => isDotnetTool
            ? """
                {
                  "packageTypes": [
                    { "name": "DotnetTool" }
                  ]
                }
                """
            : "{}";

    private static string SearchResponseJson(string packageId, long totalDownloads) => $$"""
        {
          "totalHits": 1,
          "data": [
            {
              "id": "{{packageId}}",
              "totalDownloads": {{totalDownloads}}
            }
          ]
        }
        """;

    private static string SearchEmptyJson() => """
        {
          "totalHits": 0,
          "data": []
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

