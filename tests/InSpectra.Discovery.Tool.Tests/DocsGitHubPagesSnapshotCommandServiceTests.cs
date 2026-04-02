namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Docs.Services;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using Xunit;

public sealed class DocsGitHubPagesSnapshotCommandServiceTests
{
    [Fact]
    public async Task BuildGitHubPagesSnapshotAsync_PublishesOnlySupportedFiles_WithPreservedStructure()
    {
        Runtime.Initialize();

        using var tempDirectory = new TestTemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        RepositoryPathResolver.WriteTextFile(
            Path.Combine(repositoryRoot, "index", "index.json"),
            """
            {
              "schemaVersion": 1,
              "packages": [
                {
                  "packageId": "Sample.Tool"
                }
              ]
            }
            """);
        RepositoryPathResolver.WriteTextFile(
            Path.Combine(repositoryRoot, "index", "index.min.json"),
            """
            {
              "schemaVersion": 1,
              "packages": [
                {
                  "packageId": "Sample.Tool"
                }
              ]
            }
            """);
        RepositoryPathResolver.WriteTextFile(
            Path.Combine(repositoryRoot, "index", "all.json"),
            """
            {
              "packages": []
            }
            """);
        RepositoryPathResolver.WriteTextFile(
            Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "latest", "metadata.json"),
            """
            {
              "packageId": "Sample.Tool",
              "version": "1.2.3"
            }
            """);
        RepositoryPathResolver.WriteTextFile(
            Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "latest", "opencli.json"),
            """
            {
              "opencli": "0.1-draft",
              "commands": []
            }
            """);
        RepositoryPathResolver.WriteTextFile(
            Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "latest", "crawl.json"),
            """
            {
              "documents": []
            }
            """);

        var service = new DocsCommandService();
        var exitCode = await service.BuildGitHubPagesSnapshotAsync(
            repositoryRoot,
            "index",
            "artifacts/github-pages",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var outputRoot = Path.Combine(repositoryRoot, "artifacts", "github-pages");
        Assert.True(File.Exists(Path.Combine(outputRoot, ".nojekyll")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "index.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "index.min.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "packages", "sample.tool", "latest", "metadata.json")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "packages", "sample.tool", "latest", "opencli.json")));
        Assert.False(File.Exists(Path.Combine(outputRoot, "all.json")));
        Assert.False(File.Exists(Path.Combine(outputRoot, "packages", "sample.tool", "latest", "crawl.json")));

        Assert.Equal(
            """{"schemaVersion":1,"packages":[{"packageId":"Sample.Tool"}]}""",
            File.ReadAllText(Path.Combine(outputRoot, "index.json")));
        Assert.Equal(
            """{"schemaVersion":1,"packages":[{"packageId":"Sample.Tool"}]}""",
            File.ReadAllText(Path.Combine(outputRoot, "index.min.json")));
        Assert.Equal(
            """{"packageId":"Sample.Tool","version":"1.2.3"}""",
            File.ReadAllText(Path.Combine(outputRoot, "packages", "sample.tool", "latest", "metadata.json")));
        Assert.Equal(
            """{"opencli":"0.1-draft","commands":[]}""",
            File.ReadAllText(Path.Combine(outputRoot, "packages", "sample.tool", "latest", "opencli.json")));
    }

    [Fact]
    public async Task BuildGitHubPagesSnapshotAsync_Throws_WhenIncludedJsonIsInvalid()
    {
        Runtime.Initialize();

        using var tempDirectory = new TestTemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteTextFile(
            Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "latest", "metadata.json"),
            "{not valid json");

        var service = new DocsCommandService();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildGitHubPagesSnapshotAsync(
            repositoryRoot,
            "index",
            "artifacts/github-pages",
            json: true,
            CancellationToken.None));

        Assert.Contains("metadata.json", exception.Message, StringComparison.Ordinal);
    }
}
