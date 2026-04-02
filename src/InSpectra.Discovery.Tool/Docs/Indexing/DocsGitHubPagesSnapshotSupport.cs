namespace InSpectra.Discovery.Tool.Docs.Indexing;

using InSpectra.Discovery.Tool.Infrastructure.Json;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class DocsGitHubPagesSnapshotSupport
{
    private static readonly HashSet<string> PublishedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "index.json",
        "metadata.json",
        "opencli.json",
    };

    public static async Task<DocsGitHubPagesSnapshotResult> BuildAsync(
        string sourceRoot,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Source root '{sourceRoot}' does not exist.");
        }

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);

        var publishedFileCount = 0;
        var sourceFiles = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => PublishedFileNames.Contains(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            var destinationPath = Path.Combine(outputRoot, relativePath);
            var content = await File.ReadAllTextAsync(sourceFile, cancellationToken);
            JsonNode node;

            try
            {
                node = JsonNode.Parse(content) ?? throw new InvalidOperationException();
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                throw new InvalidOperationException($"JSON file '{sourceFile}' is invalid.", ex);
            }

            RepositoryPathResolver.EnsureParentDirectory(destinationPath);
            await File.WriteAllTextAsync(
                destinationPath,
                node.ToJsonString(JsonOptions.MinifiedRepositoryFiles),
                new UTF8Encoding(false),
                cancellationToken);

            publishedFileCount++;
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputRoot, ".nojekyll"),
            string.Empty,
            new UTF8Encoding(false),
            cancellationToken);

        return new DocsGitHubPagesSnapshotResult(sourceRoot, outputRoot, publishedFileCount);
    }
}

internal sealed record DocsGitHubPagesSnapshotResult(
    string SourceRoot,
    string OutputRoot,
    int PublishedFileCount);
