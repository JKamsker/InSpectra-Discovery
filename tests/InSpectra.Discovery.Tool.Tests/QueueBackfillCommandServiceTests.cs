namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Queue.Backfill;

using System.Text.Json.Nodes;
using Xunit;

public sealed class QueueBackfillCommandServiceTests
{
    [Fact]
    public async Task BuildCurrentAnalysisBackfillQueueAsync_SelectsEligibleCurrentPackagesByDownloadRank()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json"),
            new JsonObject
            {
                ["generatedAtUtc"] = "2026-03-28T00:00:00Z",
                ["packageType"] = "DotnetTool",
                ["packageCount"] = 6,
                ["source"] = new JsonObject
                {
                    ["serviceIndexUrl"] = "https://api.nuget.org/v3/index.json",
                    ["autocompleteUrl"] = "https://example.test/autocomplete",
                    ["searchUrl"] = "https://example.test/query",
                    ["registrationBaseUrl"] = "https://api.nuget.org/v3/registration5-gz-semver2/",
                    ["prefixAlphabet"] = "abc",
                    ["expectedPackageCount"] = 6,
                    ["sortOrder"] = "totalDownloads-desc",
                },
                ["packages"] = new JsonArray
                {
                    CreateSnapshotPackage("Healthy.Tool", "1.0.0", 9000),
                    CreateSnapshotPackage("Missing.Tool", "1.0.0", 8000),
                    CreateSnapshotPackage("LegacyNegative.Tool", "1.0.0", 7000),
                    CreateSnapshotPackage("LegacyFailure.Tool", "1.0.0", 6000),
                    CreateSnapshotPackage("Retryable.Tool", "1.0.0", 5000),
                    CreateSnapshotPackage("Deferred.Tool", "1.0.0", 4000),
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "packages", "healthy.tool", "1.0.0.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Healthy.Tool",
                ["version"] = "1.0.0",
                ["currentStatus"] = "success",
                ["lastDisposition"] = "success",
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "packages", "legacynegative.tool", "1.0.0.json"),
            CreateLegacyNegativeState("LegacyNegative.Tool", "1.0.0"));
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "packages", "legacyfailure.tool", "1.0.0.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "LegacyFailure.Tool",
                ["version"] = "1.0.0",
                ["currentStatus"] = "terminal-failure",
                ["lastDisposition"] = "terminal-failure",
                ["lastFailureSignature"] = "opencli|unsupported-command|Unknown command",
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "packages", "retryable.tool", "1.0.0.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Retryable.Tool",
                ["version"] = "1.0.0",
                ["currentStatus"] = "retryable-failure",
                ["lastDisposition"] = "retryable-failure",
                ["nextAttemptAt"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"),
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "packages", "deferred.tool", "1.0.0.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Deferred.Tool",
                ["version"] = "1.0.0",
                ["currentStatus"] = "retryable-failure",
                ["lastDisposition"] = "retryable-failure",
                ["nextAttemptAt"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
            });

        var service = new QueueBackfillCommandService();
        var outputPath = Path.Combine(repositoryRoot, "artifacts", "current-backfill.queue.json");
        var exitCode = await service.BuildCurrentAnalysisBackfillQueueAsync(
            repositoryRoot,
            "state/discovery/dotnet-tools.current.json",
            outputPath,
            take: 4,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var queue = ParseJsonObject(outputPath);
        Assert.Equal("current-analysis-backfill", queue["filter"]?.GetValue<string>());
        var items = queue["items"]?.AsArray().OfType<JsonObject>().ToArray()
            ?? throw new InvalidOperationException("Expected queue items.");
        Assert.Equal(4, items.Length);
        Assert.Equal("Missing.Tool", items[0]["packageId"]?.GetValue<string>());
        Assert.Equal("LegacyNegative.Tool", items[1]["packageId"]?.GetValue<string>());
        Assert.Equal("LegacyFailure.Tool", items[2]["packageId"]?.GetValue<string>());
        Assert.Equal("Retryable.Tool", items[3]["packageId"]?.GetValue<string>());
        Assert.Equal("missing-current-analysis", items[0]["changeKind"]?.GetValue<string>());
        Assert.Equal("legacy-terminal-negative-reanalysis", items[1]["changeKind"]?.GetValue<string>());
        Assert.Equal("legacy-terminal-failure-reanalysis", items[2]["changeKind"]?.GetValue<string>());
        Assert.Equal("retryable-current-reanalysis", items[3]["changeKind"]?.GetValue<string>());
    }

    private static JsonObject CreateSnapshotPackage(string packageId, string version, long totalDownloads)
        => new()
        {
            ["packageId"] = packageId,
            ["latestVersion"] = version,
            ["totalDownloads"] = totalDownloads,
            ["versionCount"] = 1,
            ["listed"] = true,
            ["publishedAtUtc"] = "2026-03-28T00:00:00Z",
            ["commitTimestampUtc"] = "2026-03-28T00:00:00Z",
            ["projectUrl"] = $"https://example.test/{packageId}",
            ["packageUrl"] = $"https://www.nuget.org/packages/{packageId}/{version}",
            ["packageContentUrl"] = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/{version.ToLowerInvariant()}/{packageId.ToLowerInvariant()}.{version.ToLowerInvariant()}.nupkg",
            ["registrationUrl"] = $"https://api.nuget.org/v3/registration5-gz-semver2/{packageId.ToLowerInvariant()}/index.json",
            ["catalogEntryUrl"] = $"https://api.nuget.org/v3/catalog0/data/{packageId.ToLowerInvariant()}.{version.ToLowerInvariant()}.json",
        };

    private static JsonObject CreateLegacyNegativeState(string packageId, string version)
        => new()
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["currentStatus"] = "terminal-negative",
            ["lastDisposition"] = "terminal-negative",
            ["attemptCount"] = 1,
            ["lastFailureSignature"] = null,
            ["lastFailureMessage"] = null,
        };

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"inspectra-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}


