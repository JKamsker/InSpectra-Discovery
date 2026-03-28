using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliDocumentValidatorTests
{
    [Fact]
    public void TryLoadValidDocument_Rejects_Missing_OpenCli_Marker()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["info"] = new JsonObject
                {
                    ["title"] = "sample",
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact is missing the root 'opencli' marker.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Accepts_Minimal_OpenCli_Object()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out var document, out var reason);

        Assert.True(valid);
        Assert.NotNull(document);
        Assert.Null(reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_NonObject_Command_Entries()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    JsonValue.Create("serve"),
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-object entry at '$.commands[0]'.", reason);
    }

    [Fact]
    public void TryLoadValidDocument_Rejects_NonString_Example_Entries()
    {
        using var tempDirectory = new TemporaryDirectory();
        var artifactPath = Path.Combine(tempDirectory.Path, "opencli.json");
        RepositoryPathResolver.WriteJsonFile(
            artifactPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "serve",
                        ["examples"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["command"] = "serve",
                            },
                        },
                    },
                },
            });

        var valid = OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out var reason);

        Assert.False(valid);
        Assert.Equal("OpenCLI artifact has a non-string entry at '$.commands[0].examples[0]'.", reason);
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
