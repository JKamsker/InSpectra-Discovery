using System.Text.Json.Nodes;
using Xunit;

public sealed class DocsCommandServiceTests
{
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

        Assert.Equal(1234L, package["totalDownloads"]?.GetValue<long>());
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

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
