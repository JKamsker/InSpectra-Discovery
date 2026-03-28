using System.Text.Json.Nodes;

internal sealed class NativeOpenCliArtifactRegenerator
{
    public NativeOpenCliArtifactRegenerationResult RegenerateRepository(string repositoryRoot)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var packagesRoot = Path.Combine(root, "index", "packages");
        if (!Directory.Exists(packagesRoot))
        {
            return new NativeOpenCliArtifactRegenerationResult(0, 0, 0, 0, 0, [], []);
        }

        var candidates = EnumerateCandidates(root, packagesRoot).ToList();
        var rewritten = new List<string>();
        var failed = new List<string>();
        var unchangedCount = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                var existing = JsonNode.Parse(File.ReadAllText(candidate.OpenCliPath))?.AsObject()
                    ?? throw new InvalidOperationException($"OpenCLI artifact '{candidate.OpenCliPath}' is empty.");
                var sanitized = existing.DeepClone()?.AsObject()
                    ?? throw new InvalidOperationException($"OpenCLI artifact '{candidate.OpenCliPath}' could not be cloned.");
                OpenCliDocumentSanitizer.EnsureArtifactSource(sanitized, "tool-output");
                OpenCliDocumentSanitizer.Sanitize(sanitized);

                var openCliChanged = !JsonNode.DeepEquals(existing, sanitized);
                if (openCliChanged)
                {
                    RepositoryPathResolver.WriteJsonFile(candidate.OpenCliPath, sanitized);
                }

                var metadataChanged = OpenCliArtifactMetadataRepair.SyncMetadata(
                    root,
                    candidate.MetadataPath,
                    candidate.OpenCliPath,
                    "tool-output");
                if (!openCliChanged && !metadataChanged)
                {
                    unchangedCount++;
                    continue;
                }

                rewritten.Add(candidate.DisplayName);
            }
            catch (Exception ex)
            {
                failed.Add($"{candidate.DisplayName}: {ex.Message}");
            }
        }

        return new NativeOpenCliArtifactRegenerationResult(
            ScannedCount: Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
                .Count(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase)),
            CandidateCount: candidates.Count,
            RewrittenCount: rewritten.Count,
            UnchangedCount: unchangedCount,
            FailedCount: failed.Count,
            RewrittenItems: rewritten,
            FailedItems: failed);
    }

    private static IEnumerable<NativeOpenCliArtifactCandidate> EnumerateCandidates(string repositoryRoot, string packagesRoot)
        => Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => TryCreateCandidate(repositoryRoot, path))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!);

    private static NativeOpenCliArtifactCandidate? TryCreateCandidate(string repositoryRoot, string metadataPath)
    {
        if (JsonNode.Parse(File.ReadAllText(metadataPath)) is not JsonObject metadata)
        {
            return null;
        }

        var packageId = metadata["packageId"]?.GetValue<string>();
        var version = metadata["version"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var versionDirectory = Path.GetDirectoryName(metadataPath);
        if (string.IsNullOrWhiteSpace(versionDirectory))
        {
            return null;
        }

        var artifacts = metadata["artifacts"] as JsonObject;
        var steps = metadata["steps"] as JsonObject;
        var openCliStep = steps?["opencli"] as JsonObject;
        var openCliRelativePath = artifacts?["opencliPath"]?.GetValue<string>();
        var openCliPath = string.IsNullOrWhiteSpace(openCliRelativePath)
            ? Path.Combine(versionDirectory, "opencli.json")
            : Path.Combine(repositoryRoot, openCliRelativePath);
        if (!File.Exists(openCliPath))
        {
            return null;
        }

        var openCli = JsonNode.Parse(File.ReadAllText(openCliPath))?.AsObject();
        var artifactSource = openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>()
            ?? artifacts?["opencliSource"]?.GetValue<string>()
            ?? openCliStep?["artifactSource"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(artifactSource)
            && !string.Equals(artifactSource, "tool-output", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(artifactSource)
            && (!string.IsNullOrWhiteSpace(artifacts?["crawlPath"]?.GetValue<string>())
                || !string.IsNullOrWhiteSpace(artifacts?["xmldocPath"]?.GetValue<string>())))
        {
            return null;
        }

        return new NativeOpenCliArtifactCandidate(packageId, version, metadataPath, openCliPath);
    }
}

internal sealed record NativeOpenCliArtifactRegenerationResult(
    int ScannedCount,
    int CandidateCount,
    int RewrittenCount,
    int UnchangedCount,
    int FailedCount,
    IReadOnlyList<string> RewrittenItems,
    IReadOnlyList<string> FailedItems);

internal sealed record NativeOpenCliArtifactCandidate(
    string PackageId,
    string Version,
    string MetadataPath,
    string OpenCliPath)
{
    public string DisplayName => $"{PackageId} {Version}";
}
