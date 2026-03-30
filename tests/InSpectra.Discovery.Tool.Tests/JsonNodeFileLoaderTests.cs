namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class JsonNodeFileLoaderTests
{
    [Fact]
    public async Task TryLoadJsonObjectAsync_Returns_Object_For_Valid_File()
    {
        using var tempDirectory = new TemporaryDirectory();
        var path = Path.Combine(tempDirectory.Path, "object.json");
        RepositoryPathResolver.WriteJsonFile(path, new JsonObject { ["name"] = "sample" });

        var document = await JsonNodeFileLoader.TryLoadJsonObjectAsync(path, CancellationToken.None);

        Assert.Equal("sample", document?["name"]?.GetValue<string>());
    }

    [Fact]
    public async Task TryLoadJsonArrayAsync_Returns_Array_For_Valid_File()
    {
        using var tempDirectory = new TemporaryDirectory();
        var path = Path.Combine(tempDirectory.Path, "array.json");
        RepositoryPathResolver.WriteJsonFile(path, new JsonArray("a", "b"));

        var document = await JsonNodeFileLoader.TryLoadJsonArrayAsync(path, CancellationToken.None);

        Assert.Equal(2, document?.Count);
    }

    [Fact]
    public void TryLoadJsonNode_Returns_Null_For_Invalid_Or_Missing_File()
    {
        using var tempDirectory = new TemporaryDirectory();
        var missingPath = Path.Combine(tempDirectory.Path, "missing.json");
        var invalidPath = Path.Combine(tempDirectory.Path, "invalid.json");
        RepositoryPathResolver.WriteTextFile(invalidPath, "{ invalid json");

        Assert.Null(JsonNodeFileLoader.TryLoadJsonNode(missingPath));
        Assert.Null(JsonNodeFileLoader.TryLoadJsonNode(invalidPath));
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

