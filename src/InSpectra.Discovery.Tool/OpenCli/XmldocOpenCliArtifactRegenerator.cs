using System.Text.Json.Nodes;
using System.Xml.Linq;

internal sealed class XmldocOpenCliArtifactRegenerator
{
    public XmldocOpenCliArtifactRegenerationResult RegenerateRepository(
        string repositoryRoot,
        ArtifactRegenerationScope? scope = null,
        bool rebuildIndexes = true)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var packagesRoot = Path.Combine(root, "index", "packages");
        if (!Directory.Exists(packagesRoot))
        {
            return new XmldocOpenCliArtifactRegenerationResult(0, 0, 0, 0, 0, [], []);
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

        return new XmldocOpenCliArtifactRegenerationResult(
            ScannedCount: metadataPaths.Count,
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
        var crawlRelativePath = artifacts?["crawlPath"]?.GetValue<string>();
        var openCliRelativePath = artifacts?["opencliPath"]?.GetValue<string>();
        var artifactSource = artifacts?["opencliSource"]?.GetValue<string>()
            ?? openCliStep?["artifactSource"]?.GetValue<string>();
        var versionDirectory = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidOperationException($"Metadata path '{metadataPath}' did not resolve to a directory.");
        var openCliPath = string.IsNullOrWhiteSpace(openCliRelativePath)
            ? Path.Combine(versionDirectory, "opencli.json")
            : Path.Combine(repositoryRoot, openCliRelativePath);
        var openCliExists = File.Exists(openCliPath);
        var openCli = TryLoadJsonNode(openCliPath) as JsonObject;
        var hasLoadableOpenCli = openCli is not null;
        if (!string.Equals(artifactSource, "synthesized-from-xmldoc", StringComparison.OrdinalIgnoreCase)
            && openCliExists)
        {
            artifactSource = TryReadArtifactSource(openCli)
                ?? artifactSource;
        }

        var xmlDocPath = string.IsNullOrWhiteSpace(xmlDocRelativePath)
            ? null
            : Path.Combine(repositoryRoot, xmlDocRelativePath);
        var hasXmlDoc = !string.IsNullOrWhiteSpace(xmlDocPath) && File.Exists(xmlDocPath);
        var crawlPath = string.IsNullOrWhiteSpace(crawlRelativePath)
            ? null
            : Path.Combine(repositoryRoot, crawlRelativePath);
        var hasCrawl = !string.IsNullOrWhiteSpace(crawlPath) && File.Exists(crawlPath);
        var hasStaleCrawlReference = !string.IsNullOrWhiteSpace(crawlRelativePath) && !hasCrawl;
        var shouldBackfillMissingOpenCli = string.IsNullOrWhiteSpace(openCliRelativePath)
            && hasXmlDoc
            && !hasCrawl
            && !hasLoadableOpenCli;
        var shouldRepairBlankXmldocProvenance = hasXmlDoc
            && !hasCrawl
            && string.IsNullOrWhiteSpace(artifactSource)
            && (!hasLoadableOpenCli || hasStaleCrawlReference);
        if (!string.Equals(artifactSource, "synthesized-from-xmldoc", StringComparison.OrdinalIgnoreCase)
            && !shouldBackfillMissingOpenCli
            && !shouldRepairBlankXmldocProvenance)
        {
            return null;
        }

        if (!hasXmlDoc)
        {
            return null;
        }

        if (xmlDocPath is null)
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

    private static string? TryReadArtifactSource(JsonObject? document)
    {
        return document?["x-inspectra"]?["artifactSource"]?.GetValue<string>();
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
    string MetadataPath,
    string XmlDocPath,
    string OpenCliPath)
{
    public string DisplayName => $"{PackageId} {Version}";
}
