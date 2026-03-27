using System.Text.Json.Nodes;
using Xunit;

public sealed class DocsCommandServiceTests
{
    [Fact]
    public async Task RebuildIndexesAsync_ProjectsPackageLinksIntoSummaries()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json"),
            new JsonObject
            {
                ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                ["packageType"] = "DotnetTool",
                ["packageCount"] = 1,
                ["packages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["latestVersion"] = "1.2.3",
                        ["totalDownloads"] = 1234,
                        ["projectUrl"] = "https://github.com/example/sample.tool",
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3", "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["trusted"] = false,
                ["status"] = "partial",
                ["evaluatedAt"] = "2026-03-27T01:00:00Z",
                ["publishedAt"] = "2026-03-27T00:30:00Z",
                ["command"] = "sample",
                ["timings"] = new JsonObject
                {
                    ["totalMs"] = 100,
                },
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sample.tool/1.2.3/metadata.json",
                    ["opencliPath"] = null,
                    ["opencliSource"] = null,
                    ["xmldocPath"] = null,
                },
            });

        var service = new DocsCommandService();
        var exitCode = await service.RebuildIndexesAsync(
            repositoryRoot,
            writeBrowserIndex: true,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var packageIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "index.json"));
        Assert.Equal(1234L, packageIndex["totalDownloads"]?.GetValue<long>());
        Assert.Equal("https://www.nuget.org/packages/Sample.Tool", packageIndex["links"]?["nuget"]?.GetValue<string>());
        Assert.Equal("https://github.com/example/sample.tool", packageIndex["links"]?["project"]?.GetValue<string>());
        Assert.Equal("https://github.com/example/sample.tool", packageIndex["links"]?["source"]?.GetValue<string>());

        var allIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "all.json"));
        var allIndexPackage = allIndex["packages"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one package in all index.");
        Assert.NotNull(allIndex["createdAt"]?.GetValue<string>());
        Assert.NotNull(allIndex["updatedAt"]?.GetValue<string>());
        Assert.Equal("https://github.com/example/sample.tool", allIndexPackage["links"]?["source"]?.GetValue<string>());

        Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "latest", "metadata.json")));
        var browserIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "index.json"));
        Assert.NotNull(browserIndex["createdAt"]?.GetValue<string>());
        Assert.NotNull(browserIndex["updatedAt"]?.GetValue<string>());
    }

    [Fact]
    public async Task BuildBrowserIndexAsync_PreservesTotalDownloadsFromAllIndex()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "all.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["createdAt"] = "2026-03-20T00:00:00Z",
                ["generatedAt"] = "2026-03-27T00:00:00Z",
                ["packageCount"] = 1,
                ["packages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["totalDownloads"] = 1234,
                        ["latestVersion"] = "1.2.3",
                        ["latestStatus"] = "ok",
                        ["commandCount"] = 7,
                        ["commandGroupCount"] = 2,
                        ["versions"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["version"] = "1.2.3",
                                ["command"] = "sample",
                            },
                        },
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "index.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["generatedAt"] = "2026-03-21T00:00:00Z",
                ["packageCount"] = 0,
                ["packages"] = new JsonArray(),
            });

        var service = new DocsCommandService();
        var exitCode = await service.BuildBrowserIndexAsync(
            repositoryRoot,
            "index/all.json",
            "index/index.json",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var browserIndex = JsonNode.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "index", "index.json")))?.AsObject()
            ?? throw new InvalidOperationException("Generated browser index was empty.");
        var package = browserIndex["packages"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one package in browser index.");

        Assert.Equal(
            DateTimeOffset.Parse("2026-03-21T00:00:00Z"),
            DateTimeOffset.Parse(browserIndex["createdAt"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing createdAt.")));
        Assert.NotNull(browserIndex["updatedAt"]?.GetValue<string>());
        Assert.Equal(1234L, package["totalDownloads"]?.GetValue<long>());
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

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

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
