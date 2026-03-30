namespace InSpectra.Discovery.Tool.Tests;

using Xunit;

public sealed class RepositoryPathResolverTests
{
    [Fact]
    public void WriteJsonFile_PreservesPlusInIsoOffsetStrings()
    {
        var root = Path.Combine(Path.GetTempPath(), "inspectra-repository-path-resolver-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "sample.json");

        try
        {
            RepositoryPathResolver.WriteJsonFile(path, new
            {
                PublishedAt = "2026-01-12T14:13:15.5370000+00:00",
            });

            var contents = File.ReadAllText(path);

            Assert.Contains("\"publishedAt\": \"2026-01-12T14:13:15.5370000+00:00\"", contents);
            Assert.DoesNotContain("\\u002B", contents);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

