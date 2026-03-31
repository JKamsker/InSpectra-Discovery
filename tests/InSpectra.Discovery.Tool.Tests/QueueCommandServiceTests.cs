namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Queue.Backfill;
using InSpectra.Discovery.Tool.Queue.Planning;

using InSpectra.Discovery.Tool.Analysis.Tools;

using System.Text.Json.Nodes;
using Xunit;

public sealed class QueueCommandServiceTests
{
    [Fact]
    public async Task BuildUntrustedBatchPlanAsync_EnrichesItemsWithAnalysisMetadata()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "discovery", "queue.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["sourceCurrentSnapshotPath"] = "state/discovery/dotnet-tools.current.json",
                ["skipRunnerInspection"] = true,
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["version"] = "1.2.3",
                        ["totalDownloads"] = 1234,
                        ["packageUrl"] = "https://www.nuget.org/packages/Sample.Tool/1.2.3",
                        ["packageContentUrl"] = "https://nuget.test/sample.tool.1.2.3.nupkg",
                        ["catalogEntryUrl"] = "https://nuget.test/catalog/sample.tool.1.2.3.json",
                        ["dotnetSetupMode"] = "legacy-multi-sdk",
                        ["dotnetSetupSource"] = "test",
                        ["requiredDotnetRuntimes"] = new JsonArray(),
                    },
                },
            });

        var service = new QueueCommandService(new FakeDescriptorResolver(
            new ToolDescriptor(
                "Sample.Tool",
                "1.2.3",
                "sample",
                "System.CommandLine",
                "help",
                "generic-help-crawl",
                "https://www.nuget.org/packages/Sample.Tool/1.2.3",
                "https://nuget.test/sample.tool.1.2.3.nupkg",
                "https://nuget.test/catalog/sample.tool.1.2.3.json")));

        var exitCode = await service.BuildUntrustedBatchPlanAsync(
            repositoryRoot,
            "state/discovery/queue.json",
            "batch-001",
            Path.Combine(repositoryRoot, "artifacts", "expected.json"),
            0,
            null,
            forceReanalyze: false,
            targetBranch: "main",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var plan = ParseJsonObject(Path.Combine(repositoryRoot, "artifacts", "expected.json"));
        var item = plan["items"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one batch item.");
        Assert.Equal("sample", item["command"]?.GetValue<string>());
        Assert.Equal("System.CommandLine", item["cliFramework"]?.GetValue<string>());
        Assert.Equal("help", item["analysisMode"]?.GetValue<string>());
        Assert.Equal("generic-help-crawl", item["analysisReason"]?.GetValue<string>());
        Assert.Equal("legacy-multi-sdk", item["dotnetSetupMode"]?.GetValue<string>());
    }

    [Fact]
    public async Task BuildUntrustedBatchPlanAsync_FallsBackToAutoMode_WhenAnalysisDescriptorResolutionFails()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "discovery", "queue.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["sourceCurrentSnapshotPath"] = "state/discovery/dotnet-tools.current.json",
                ["skipRunnerInspection"] = true,
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Broken.Tool",
                        ["version"] = "0.1.0",
                        ["packageUrl"] = "https://www.nuget.org/packages/Broken.Tool/0.1.0",
                        ["packageContentUrl"] = "https://nuget.test/broken.tool.0.1.0.nupkg",
                        ["catalogEntryUrl"] = "https://nuget.test/catalog/broken.tool.0.1.0.json",
                        ["dotnetSetupMode"] = "legacy-multi-sdk",
                        ["dotnetSetupSource"] = "test",
                        ["requiredDotnetRuntimes"] = new JsonArray(),
                    },
                },
            });

        var service = new QueueCommandService(new ThrowingDescriptorResolver("resolver failed"));

        var exitCode = await service.BuildUntrustedBatchPlanAsync(
            repositoryRoot,
            "state/discovery/queue.json",
            "batch-002",
            Path.Combine(repositoryRoot, "artifacts", "expected.json"),
            0,
            null,
            forceReanalyze: false,
            targetBranch: "main",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var plan = ParseJsonObject(Path.Combine(repositoryRoot, "artifacts", "expected.json"));
        var item = plan["items"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one batch item.");
        Assert.Equal("auto", item["analysisMode"]?.GetValue<string>());
        Assert.Equal("resolver failed", item["analysisReason"]?.GetValue<string>());
    }

    [Fact]
    public async Task BuildUntrustedBatchPlanAsync_RequeuesLegacyTerminalNegativeStates()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "discovery", "queue.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["sourceCurrentSnapshotPath"] = "state/discovery/dotnet-tools.current.json",
                ["skipRunnerInspection"] = true,
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Legacy.Tool",
                        ["version"] = "2.0.0",
                        ["packageUrl"] = "https://www.nuget.org/packages/Legacy.Tool/2.0.0",
                        ["packageContentUrl"] = "https://nuget.test/legacy.tool.2.0.0.nupkg",
                        ["catalogEntryUrl"] = "https://nuget.test/catalog/legacy.tool.2.0.0.json",
                        ["dotnetSetupMode"] = "legacy-multi-sdk",
                        ["dotnetSetupSource"] = "test",
                        ["requiredDotnetRuntimes"] = new JsonArray(),
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "packages", "legacy.tool", "2.0.0.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Legacy.Tool",
                ["version"] = "2.0.0",
                ["currentStatus"] = "terminal-negative",
                ["lastDisposition"] = "terminal-negative",
                ["attemptCount"] = 1,
                ["lastFailureSignature"] = null,
                ["lastFailureMessage"] = null,
            });

        var service = new QueueCommandService(new FakeDescriptorResolver(
            new ToolDescriptor(
                "Legacy.Tool",
                "2.0.0",
                "legacy",
                "System.CommandLine",
                "help",
                "generic-help-crawl",
                "https://www.nuget.org/packages/Legacy.Tool/2.0.0",
                "https://nuget.test/legacy.tool.2.0.0.nupkg",
                "https://nuget.test/catalog/legacy.tool.2.0.0.json")));

        var exitCode = await service.BuildUntrustedBatchPlanAsync(
            repositoryRoot,
            "state/discovery/queue.json",
            "batch-003",
            Path.Combine(repositoryRoot, "artifacts", "expected.json"),
            0,
            null,
            forceReanalyze: false,
            targetBranch: "main",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var plan = ParseJsonObject(Path.Combine(repositoryRoot, "artifacts", "expected.json"));
        Assert.Single(plan["items"]?.AsArray().OfType<JsonObject>() ?? []);
        Assert.Empty(plan["skipped"]?.AsArray() ?? []);
    }

    [Fact]
    public async Task BuildLegacyTerminalNegativeQueueAsync_SelectsCurrentLegacyNegativesByDownloadRank()
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
                ["packageCount"] = 3,
                ["source"] = new JsonObject
                {
                    ["serviceIndexUrl"] = "https://api.nuget.org/v3/index.json",
                    ["autocompleteUrl"] = "https://example.test/autocomplete",
                    ["searchUrl"] = "https://example.test/query",
                    ["registrationBaseUrl"] = "https://api.nuget.org/v3/registration5-gz-semver2/",
                    ["prefixAlphabet"] = "abc",
                    ["expectedPackageCount"] = 3,
                    ["sortOrder"] = "totalDownloads-desc",
                },
                ["packages"] = new JsonArray
                {
                    CreateSnapshotPackage("Popular.Tool", "3.0.0", 5000),
                    CreateSnapshotPackage("LessPopular.Tool", "2.0.0", 2500),
                    CreateSnapshotPackage("Healthy.Tool", "1.0.0", 9000),
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "packages", "popular.tool", "3.0.0.json"),
            CreateLegacyNegativeState("Popular.Tool", "3.0.0"));
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "packages", "lesspopular.tool", "2.0.0.json"),
            CreateLegacyNegativeState("LessPopular.Tool", "2.0.0"));
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

        var service = new QueueBackfillCommandService();
        var outputPath = Path.Combine(repositoryRoot, "artifacts", "legacy-negative.queue.json");
        var exitCode = await service.BuildLegacyTerminalNegativeQueueAsync(
            repositoryRoot,
            "state/discovery/dotnet-tools.current.json",
            outputPath,
            take: 1,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var queue = ParseJsonObject(outputPath);
        Assert.Equal("legacy-terminal-negative", queue["filter"]?.GetValue<string>());
        Assert.Equal(1, queue["itemCount"]?.GetValue<int>());
        var item = queue["items"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one queued item.");
        Assert.Equal("Popular.Tool", item["packageId"]?.GetValue<string>());
        Assert.Equal("3.0.0", item["version"]?.GetValue<string>());
        Assert.Equal("legacy-terminal-negative-reanalysis", item["changeKind"]?.GetValue<string>());
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

    private sealed class FakeDescriptorResolver(ToolDescriptor descriptor) : IToolDescriptorResolver
    {
        public Task<ToolDescriptor> ResolveAsync(string packageId, string version, CancellationToken cancellationToken)
            => Task.FromResult(descriptor);
    }

    private sealed class ThrowingDescriptorResolver(string message) : IToolDescriptorResolver
    {
        public Task<ToolDescriptor> ResolveAsync(string packageId, string version, CancellationToken cancellationToken)
            => throw new InvalidOperationException(message);
    }

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
