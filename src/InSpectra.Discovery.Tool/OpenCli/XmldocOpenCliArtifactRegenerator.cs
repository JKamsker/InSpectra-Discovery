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
                var existing = JsonNode.Parse(File.ReadAllText(candidate.OpenCliPath));
                if (JsonNode.DeepEquals(existing, regenerated))
                {
                    unchangedCount++;
                    continue;
                }

                RepositoryPathResolver.WriteJsonFile(candidate.OpenCliPath, regenerated);
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
        return OpenCliDocumentSynthesizer.ConvertFromXmldoc(xmlDocument, candidate.Title);
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
        var artifactSource = artifacts?["opencliSource"]?.GetValue<string>()
            ?? openCliStep?["artifactSource"]?.GetValue<string>();
        if (!string.Equals(artifactSource, "synthesized-from-xmldoc", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var xmlDocRelativePath = artifacts?["xmldocPath"]?.GetValue<string>();
        var openCliRelativePath = artifacts?["opencliPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(xmlDocRelativePath) || string.IsNullOrWhiteSpace(openCliRelativePath))
        {
            return null;
        }

        var xmlDocPath = Path.Combine(repositoryRoot, xmlDocRelativePath);
        var openCliPath = Path.Combine(repositoryRoot, openCliRelativePath);
        if (!File.Exists(xmlDocPath) || !File.Exists(openCliPath))
        {
            return null;
        }

        return new XmldocOpenCliArtifactCandidate(
            packageId,
            version,
            metadata["command"]?.GetValue<string>() ?? packageId,
            xmlDocPath,
            openCliPath);
    }
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
    string XmlDocPath,
    string OpenCliPath)
{
    public string DisplayName => $"{PackageId} {Version}";
}
