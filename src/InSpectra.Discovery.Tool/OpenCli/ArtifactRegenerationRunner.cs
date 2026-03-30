namespace InSpectra.Discovery.Tool.OpenCli;

using System.Text.Json.Nodes;

internal sealed record ArtifactRegenerationRunResult(
    int ScannedCount,
    int CandidateCount,
    int RewrittenCount,
    int UnchangedCount,
    int FailedCount,
    IReadOnlyList<string> RewrittenItems,
    IReadOnlyList<string> FailedItems);

internal static class ArtifactRegenerationRunner
{
    public static ArtifactRegenerationRunResult Run<TCandidate>(
        string repositoryRoot,
        ArtifactRegenerationScope? scope,
        bool rebuildIndexes,
        Func<string, string, TCandidate?> tryCreateCandidate,
        Func<string, TCandidate, bool> processCandidate,
        Func<TCandidate, string> getDisplayName)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var packagesRoot = Path.Combine(root, "index", "packages");
        if (!Directory.Exists(packagesRoot))
        {
            return new ArtifactRegenerationRunResult(0, 0, 0, 0, 0, [], []);
        }

        var metadataPaths = ArtifactRegenerationMetadataPathSupport.EnumerateMetadataPaths(packagesRoot, scope);
        var candidates = metadataPaths
            .Select(path => tryCreateCandidate(root, path))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToList();
        var rewritten = new List<string>();
        var failed = new List<string>();
        var unchangedCount = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                if (!processCandidate(root, candidate))
                {
                    unchangedCount++;
                    continue;
                }

                rewritten.Add(getDisplayName(candidate));
            }
            catch (Exception ex)
            {
                failed.Add($"{getDisplayName(candidate)}: {ex.Message}");
            }
        }

        if (rebuildIndexes && candidates.Count > 0)
        {
            RepositoryPackageIndexBuilder.Rebuild(root, writeBrowserIndex: true);
        }

        return new ArtifactRegenerationRunResult(
            ScannedCount: metadataPaths.Count,
            CandidateCount: candidates.Count,
            RewrittenCount: rewritten.Count,
            UnchangedCount: unchangedCount,
            FailedCount: failed.Count,
            RewrittenItems: rewritten,
            FailedItems: failed);
    }
}

