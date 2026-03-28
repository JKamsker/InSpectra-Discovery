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
                var existingNode = TryLoadJsonNode(candidate.OpenCliPath);
                if (existingNode is not JsonObject existing)
                {
                    var rejectedMetadataChanged = RejectMalformedNativeArtifact(root, candidate.MetadataPath, candidate.OpenCliPath);
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

        if (candidates.Count > 0)
        {
            RepositoryPackageIndexBuilder.Rebuild(root, writeBrowserIndex: true);
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

        var openCli = TryLoadJsonObject(openCliPath);
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

    private static bool RejectMalformedNativeArtifact(string repositoryRoot, string metadataPath, string openCliPath)
    {
        var metadata = TryLoadJsonNode(metadataPath) as JsonObject;
        if (metadata is null)
        {
            return false;
        }

        var original = metadata.DeepClone();
        if (File.Exists(openCliPath))
        {
            File.Delete(openCliPath);
        }

        var artifacts = metadata["artifacts"] as JsonObject ?? new JsonObject();
        artifacts.Remove("opencliPath");
        artifacts.Remove("opencliSource");
        metadata["artifacts"] = artifacts;

        if (string.Equals(metadata["status"]?.GetValue<string>(), "ok", StringComparison.OrdinalIgnoreCase))
        {
            metadata["status"] = "partial";
        }

        var message = "Stored OpenCLI artifact is not a JSON object.";
        var steps = metadata["steps"] as JsonObject ?? new JsonObject();
        var openCliStep = steps["opencli"] as JsonObject ?? new JsonObject();
        openCliStep["status"] = "failed";
        openCliStep["classification"] = "invalid-opencli-artifact";
        openCliStep["message"] = message;
        openCliStep.Remove("path");
        openCliStep.Remove("artifactSource");
        steps["opencli"] = openCliStep;
        metadata["steps"] = steps;

        var introspection = metadata["introspection"] as JsonObject ?? new JsonObject();
        var openCliIntrospection = introspection["opencli"] as JsonObject ?? new JsonObject();
        openCliIntrospection["status"] = "invalid-output";
        openCliIntrospection["classification"] = "invalid-opencli-artifact";
        openCliIntrospection["message"] = message;
        openCliIntrospection.Remove("artifactSource");
        introspection["opencli"] = openCliIntrospection;
        metadata["introspection"] = introspection;

        if (JsonNode.DeepEquals(original, metadata))
        {
            return false;
        }

        RepositoryPathResolver.WriteJsonFile(metadataPath, metadata);
        return true;
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
