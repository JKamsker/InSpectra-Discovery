namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.Catalog.Delta;

using System.Text.Json;
using Xunit;

public sealed class DotnetToolDeltaQueueBuilderTests
{
    [Fact]
    public async Task RunAsync_QueuesOnlyCurrentChangedPackages_OrderedByDownloads()
    {
        using var tempDirectory = new TemporaryDirectory();
        var inputPath = tempDirectory.GetPath("dotnet-tools.delta.json");
        await WriteJsonAsync(
            inputPath,
            new DotnetToolDeltaSnapshot(
                DateTimeOffset.Parse("2026-03-28T12:00:00Z"),
                DateTimeOffset.Parse("2026-03-28T11:00:00Z"),
                DateTimeOffset.Parse("2026-03-28T11:00:00Z"),
                DateTimeOffset.Parse("2026-03-28T12:00:00Z"),
                "https://api.nuget.org/v3/index.json",
                "https://api.nuget.org/v3/catalog0/index.json",
                "state/discovery/dotnet-tools.current.json",
                1,
                3,
                3,
                3,
                [
                    new DotnetToolDeltaEntry("Removed.Tool", "removed", "1.0.0", null, CreateState("Removed.Tool", "1.0.0", 10), null),
                    new DotnetToolDeltaEntry("Low.Tool", "updated", "1.0.0", "1.1.0", CreateState("Low.Tool", "1.0.0", 10), CreateState("Low.Tool", "1.1.0", 10)),
                    new DotnetToolDeltaEntry("High.Tool", "added", null, "2.0.0", null, CreateState("High.Tool", "2.0.0", 100)),
                ]));

        var builder = new DotnetToolDeltaQueueBuilder();
        var computation = await builder.RunAsync(
            new IndexDeltaAllToolsOptions
            {
                InputDeltaPath = inputPath,
                OutputDeltaPath = tempDirectory.GetPath("out.delta.json"),
                QueueOutputPath = tempDirectory.GetPath("out.queue.json"),
            },
            reportProgress: null,
            CancellationToken.None);

        Assert.Equal(2, computation.Delta.PackageCount);
        Assert.Equal(2, computation.Queue.ItemCount);
        Assert.Equal("High.Tool", computation.Queue.Items[0].PackageId);
        Assert.Equal("Low.Tool", computation.Queue.Items[1].PackageId);
        Assert.DoesNotContain(computation.Queue.Items, item => item.PackageId == "Removed.Tool");
    }

    private static DotnetToolDeltaState CreateState(string packageId, string version, long downloads)
        => new(
            version,
            downloads,
            1,
            true,
            DateTimeOffset.Parse("2026-03-28T10:00:00Z"),
            DateTimeOffset.Parse("2026-03-28T10:30:00Z"),
            $"https://github.com/example/{packageId}",
            $"https://www.nuget.org/packages/{packageId}/{version}",
            $"https://nuget.test/{packageId.ToLowerInvariant()}.{version}.nupkg",
            $"https://nuget.test/registration/{packageId.ToLowerInvariant()}/index.json",
            $"https://nuget.test/catalog/{packageId.ToLowerInvariant()}.{version}.json",
            packageId,
            $"{packageId} description",
            null,
            null,
            null);

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions.Default);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"inspectra-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string GetPath(string relativePath) => System.IO.Path.Combine(Path, relativePath);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

