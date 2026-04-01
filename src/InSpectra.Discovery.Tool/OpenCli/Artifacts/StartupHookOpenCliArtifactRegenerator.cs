namespace InSpectra.Discovery.Tool.OpenCli.Artifacts;

internal sealed class StartupHookOpenCliArtifactRegenerator
{
    public StartupHookOpenCliArtifactRegenerationResult RegenerateRepository(
        string repositoryRoot,
        ArtifactRegenerationScope? scope = null,
        bool rebuildIndexes = true)
    {
        var result = ArtifactRegenerationRunner.Run(
            repositoryRoot,
            scope,
            rebuildIndexes,
            TryCreateCandidate,
            ProcessCandidate,
            static candidate => candidate.DisplayName);

        return new StartupHookOpenCliArtifactRegenerationResult(
            result.ScannedCount,
            result.CandidateCount,
            result.RewrittenCount,
            result.UnchangedCount,
            result.FailedCount,
            result.RewrittenItems,
            result.FailedItems);
    }

    private static StoredOpenCliArtifactCandidate? TryCreateCandidate(string repositoryRoot, string metadataPath)
        => StoredOpenCliArtifactCandidateFactory.TryCreateCandidate(repositoryRoot, metadataPath, "startup-hook");

    private static bool ProcessCandidate(string repositoryRoot, StoredOpenCliArtifactCandidate candidate)
        => StoredOpenCliArtifactRegenerationSupport.ProcessCandidate(repositoryRoot, candidate);
}

internal sealed record StartupHookOpenCliArtifactRegenerationResult(
    int ScannedCount,
    int CandidateCount,
    int RewrittenCount,
    int UnchangedCount,
    int FailedCount,
    IReadOnlyList<string> RewrittenItems,
    IReadOnlyList<string> FailedItems);
