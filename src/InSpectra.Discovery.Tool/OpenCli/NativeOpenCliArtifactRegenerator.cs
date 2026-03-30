using System.Text.Json.Nodes;

internal sealed class NativeOpenCliArtifactRegenerator
{
    public NativeOpenCliArtifactRegenerationResult RegenerateRepository(
        string repositoryRoot,
        ArtifactRegenerationScope? scope = null,
        bool rebuildIndexes = true)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var packagesRoot = Path.Combine(root, "index", "packages");
        if (!Directory.Exists(packagesRoot))
        {
            return new NativeOpenCliArtifactRegenerationResult(0, 0, 0, 0, 0, [], []);
        }

        var metadataPaths = ArtifactRegenerationMetadataPathSupport.EnumerateMetadataPaths(packagesRoot, scope);
        var candidates = metadataPaths
            .Select(path => TryCreateCandidate(root, path))
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
                var fileSize = new FileInfo(candidate.OpenCliPath).Length;
                if (fileSize > 2 * 1024 * 1024)
                {
                    var rejectedMetadataChanged = OpenCliArtifactRejectionSupport.RejectInvalidArtifact(
                        root,
                        candidate.MetadataPath,
                        candidate.OpenCliPath,
                        $"OpenCLI artifact is implausibly large ({fileSize / 1024 / 1024} MB).",
                        xmldocPath: candidate.XmlDocPath);
                    var rejectedStateChanged = IndexedStatePathsRepair.SyncFromMetadata(root, candidate.MetadataPath);
                    if (!rejectedMetadataChanged && !rejectedStateChanged)
                    {
                        unchangedCount++;
                        continue;
                    }

                    rewritten.Add(candidate.DisplayName);
                    continue;
                }

                var existingNode = TryLoadJsonNode(candidate.OpenCliPath);
                if (existingNode is not JsonObject existing)
                {
                    var rejectedMetadataChanged = OpenCliArtifactRejectionSupport.RejectInvalidArtifact(
                        root,
                        candidate.MetadataPath,
                        candidate.OpenCliPath,
                        "Stored OpenCLI artifact is not a JSON object.",
                        xmldocPath: candidate.XmlDocPath);
                    var rejectedStateChanged = IndexedStatePathsRepair.SyncFromMetadata(root, candidate.MetadataPath);
                    if (!rejectedMetadataChanged && !rejectedStateChanged)
                    {
                        unchangedCount++;
                        continue;
                    }

                    rewritten.Add(candidate.DisplayName);
                    continue;
                }

                var sanitized = existing.DeepClone()?.AsObject()
                    ?? throw new InvalidOperationException($"OpenCLI artifact '{candidate.OpenCliPath}' could not be cloned.");
                OpenCliDocumentSanitizer.EnsureArtifactSource(sanitized, "tool-output");
                OpenCliDocumentSanitizer.Sanitize(sanitized);

                if (!OpenCliDocumentValidator.TryValidateDocument(sanitized, out var validationError))
                {
                    var rejectedMetadataChanged = OpenCliArtifactRejectionSupport.RejectInvalidArtifact(
                        root,
                        candidate.MetadataPath,
                        candidate.OpenCliPath,
                        validationError ?? "Native OpenCLI artifact is not publishable.",
                        xmldocPath: candidate.XmlDocPath);
                    var rejectedStateChanged = IndexedStatePathsRepair.SyncFromMetadata(root, candidate.MetadataPath);
                    if (!rejectedMetadataChanged && !rejectedStateChanged)
                    {
                        unchangedCount++;
                        continue;
                    }

                    rewritten.Add(candidate.DisplayName);
                    continue;
                }

                var openCliChanged = !JsonNode.DeepEquals(existing, sanitized);
                if (openCliChanged)
                {
                    RepositoryPathResolver.WriteJsonFile(candidate.OpenCliPath, sanitized);
                }

                var metadataChanged = OpenCliArtifactMetadataRepair.SyncMetadata(
                    root,
                    candidate.MetadataPath,
                    candidate.OpenCliPath,
                    "tool-output",
                    xmldocPath: candidate.XmlDocPath);
                var stateChanged = IndexedStatePathsRepair.SyncFromMetadata(root, candidate.MetadataPath);
                if (!openCliChanged && !metadataChanged && !stateChanged)
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

        if (rebuildIndexes && candidates.Count > 0)
        {
            RepositoryPackageIndexBuilder.Rebuild(root, writeBrowserIndex: true);
        }

        return new NativeOpenCliArtifactRegenerationResult(
            ScannedCount: metadataPaths.Count,
            CandidateCount: candidates.Count,
            RewrittenCount: rewritten.Count,
            UnchangedCount: unchangedCount,
            FailedCount: failed.Count,
            RewrittenItems: rewritten,
            FailedItems: failed);
    }

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

        var openCliFileSize = new FileInfo(openCliPath).Length;
        var openCli = openCliFileSize <= 2 * 1024 * 1024
            ? TryLoadJsonObject(openCliPath)
            : null;
        var artifactSource = openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>()
            ?? artifacts?["opencliSource"]?.GetValue<string>()
            ?? openCliStep?["artifactSource"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(artifactSource)
            && !string.Equals(artifactSource, "tool-output", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var crawlRelativePath = artifacts?["crawlPath"]?.GetValue<string>();
        var crawlPath = string.IsNullOrWhiteSpace(crawlRelativePath)
            ? null
            : Path.Combine(repositoryRoot, crawlRelativePath);
        if (!File.Exists(crawlPath))
        {
            crawlPath = null;
        }

        var xmlDocRelativePath = artifacts?["xmldocPath"]?.GetValue<string>();
        var xmlDocPath = string.IsNullOrWhiteSpace(xmlDocRelativePath)
            ? null
            : Path.Combine(repositoryRoot, xmlDocRelativePath);
        if (!File.Exists(xmlDocPath))
        {
            xmlDocPath = null;
        }

        if (string.IsNullOrWhiteSpace(artifactSource)
            && (crawlPath is not null || xmlDocPath is not null))
        {
            return null;
        }

        return new NativeOpenCliArtifactCandidate(packageId, version, metadataPath, openCliPath, xmlDocPath);
    }

    private static JsonNode? TryLoadJsonNode(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static JsonObject? TryLoadJsonObject(string path)
        => TryLoadJsonNode(path) as JsonObject;
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
    string OpenCliPath,
    string? XmlDocPath)
{
    public string DisplayName => $"{PackageId} {Version}";
}
