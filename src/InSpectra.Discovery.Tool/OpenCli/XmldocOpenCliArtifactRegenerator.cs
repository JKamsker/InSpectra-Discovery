using System.Text.Json.Nodes;
using System.Xml.Linq;

internal sealed class XmldocOpenCliArtifactRegenerator
{
    public XmldocOpenCliArtifactRegenerationResult RegenerateRepository(string repositoryRoot)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var packagesRoot = Path.Combine(root, "index", "packages");
        if (!Directory.Exists(packagesRoot))
        {
            return new XmldocOpenCliArtifactRegenerationResult(0, 0, 0, 0, 0, [], []);
        }

        var candidates = EnumerateCandidates(root, packagesRoot).ToList();
        var rewritten = new List<string>();
        var failed = new List<string>();
        var unchangedCount = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                var regenerated = RegenerateOpenCli(candidate);
                var existing = TryLoadJsonNode(candidate.OpenCliPath);
                var openCliChanged = !JsonNode.DeepEquals(existing, regenerated);
                if (openCliChanged)
                {
                    RepositoryPathResolver.WriteJsonFile(candidate.OpenCliPath, regenerated);
                }

                var metadataChanged = OpenCliArtifactMetadataRepair.SyncMetadata(
                    root,
                    candidate.MetadataPath,
                    candidate.OpenCliPath,
                    "synthesized-from-xmldoc",
                    xmldocPath: candidate.XmlDocPath,
                    synthesizedArtifact: true);
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

        return new XmldocOpenCliArtifactRegenerationResult(
            ScannedCount: Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
                .Count(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase)),
            CandidateCount: candidates.Count,
            RewrittenCount: rewritten.Count,
            UnchangedCount: unchangedCount,
            FailedCount: failed.Count,
            RewrittenItems: rewritten,
            FailedItems: failed);
    }

    private static JsonObject RegenerateOpenCli(XmldocOpenCliArtifactCandidate candidate)
    {
        var xmlDocument = XDocument.Parse(File.ReadAllText(candidate.XmlDocPath));
        return OpenCliDocumentSynthesizer.ConvertFromXmldoc(xmlDocument, candidate.Title, candidate.Version);
    }

    private static IEnumerable<XmldocOpenCliArtifactCandidate> EnumerateCandidates(string repositoryRoot, string packagesRoot)
        => Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => TryCreateCandidate(repositoryRoot, path))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!);

    private static XmldocOpenCliArtifactCandidate? TryCreateCandidate(string repositoryRoot, string metadataPath)
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

        var artifacts = metadata["artifacts"] as JsonObject;
        var steps = metadata["steps"] as JsonObject;
        var openCliStep = steps?["opencli"] as JsonObject;
        var xmlDocRelativePath = artifacts?["xmldocPath"]?.GetValue<string>();
        var openCliRelativePath = artifacts?["opencliPath"]?.GetValue<string>();
        var artifactSource = artifacts?["opencliSource"]?.GetValue<string>()
            ?? openCliStep?["artifactSource"]?.GetValue<string>();
        var versionDirectory = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidOperationException($"Metadata path '{metadataPath}' did not resolve to a directory.");
        var openCliPath = string.IsNullOrWhiteSpace(openCliRelativePath)
            ? Path.Combine(versionDirectory, "opencli.json")
            : Path.Combine(repositoryRoot, openCliRelativePath);
        var openCliExists = File.Exists(openCliPath);
        if (!string.Equals(artifactSource, "synthesized-from-xmldoc", StringComparison.OrdinalIgnoreCase)
            && openCliExists)
        {
            artifactSource = TryReadArtifactSource(openCliPath)
                ?? artifactSource;
        }

        var shouldBackfillMissingOpenCli = string.IsNullOrWhiteSpace(openCliRelativePath)
            && !string.IsNullOrWhiteSpace(xmlDocRelativePath)
            && !openCliExists;
        if (!string.Equals(artifactSource, "synthesized-from-xmldoc", StringComparison.OrdinalIgnoreCase)
            && !shouldBackfillMissingOpenCli)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(xmlDocRelativePath))
        {
            return null;
        }

        var xmlDocPath = Path.Combine(repositoryRoot, xmlDocRelativePath);
        if (!File.Exists(xmlDocPath))
        {
            return null;
        }

        return new XmldocOpenCliArtifactCandidate(
            packageId,
            version,
            metadata["command"]?.GetValue<string>() ?? packageId,
            metadataPath,
            xmlDocPath,
            openCliPath);
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

    private static string? TryReadArtifactSource(string path)
        => TryLoadJsonNode(path)?["x-inspectra"]?["artifactSource"]?.GetValue<string>();
}

internal sealed record XmldocOpenCliArtifactRegenerationResult(
    int ScannedCount,
    int CandidateCount,
    int RewrittenCount,
    int UnchangedCount,
    int FailedCount,
    IReadOnlyList<string> RewrittenItems,
    IReadOnlyList<string> FailedItems);

internal sealed record XmldocOpenCliArtifactCandidate(
    string PackageId,
    string Version,
    string Title,
    string MetadataPath,
    string XmlDocPath,
    string OpenCliPath)
{
    public string DisplayName => $"{PackageId} {Version}";
}
