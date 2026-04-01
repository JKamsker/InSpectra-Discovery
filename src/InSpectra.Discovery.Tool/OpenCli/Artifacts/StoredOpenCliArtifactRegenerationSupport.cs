namespace InSpectra.Discovery.Tool.OpenCli.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Json;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Documents;

using System.Text.Json.Nodes;

internal sealed record StoredOpenCliArtifactCandidate(
    string PackageId,
    string Version,
    string MetadataPath,
    string OpenCliPath,
    string? XmlDocPath,
    string ArtifactSource)
{
    public string DisplayName => $"{PackageId} {Version}";
}

internal static class StoredOpenCliArtifactCandidateFactory
{
    private const long InlineArtifactSizeLimitBytes = 2 * 1024 * 1024;

    public static StoredOpenCliArtifactCandidate? TryCreateCandidate(
        string repositoryRoot,
        string metadataPath,
        string expectedArtifactSource,
        bool allowMissingArtifactSource = false)
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

        var openCli = TryLoadInspectableOpenCli(openCliPath);
        var artifactSource = ResolveArtifactSource(openCli, metadata, artifacts, openCliStep);
        if (string.IsNullOrWhiteSpace(artifactSource))
        {
            if (!allowMissingArtifactSource || HasDerivedArtifacts(repositoryRoot, artifacts))
            {
                return null;
            }

            artifactSource = expectedArtifactSource;
        }

        if (!string.Equals(artifactSource, expectedArtifactSource, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var xmlDocRelativePath = artifacts?["xmldocPath"]?.GetValue<string>();
        var xmlDocPath = string.IsNullOrWhiteSpace(xmlDocRelativePath)
            ? null
            : Path.Combine(repositoryRoot, xmlDocRelativePath);
        if (!File.Exists(xmlDocPath))
        {
            xmlDocPath = null;
        }

        return new StoredOpenCliArtifactCandidate(
            packageId,
            version,
            metadataPath,
            openCliPath,
            xmlDocPath,
            expectedArtifactSource);
    }

    private static JsonObject? TryLoadInspectableOpenCli(string openCliPath)
    {
        var openCliFileSize = new FileInfo(openCliPath).Length;
        return openCliFileSize <= InlineArtifactSizeLimitBytes
            ? JsonNodeFileLoader.TryLoadJsonObject(openCliPath)
            : null;
    }

    private static string? ResolveArtifactSource(
        JsonObject? openCli,
        JsonObject metadata,
        JsonObject? artifacts,
        JsonObject? openCliStep)
        => openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>()
            ?? artifacts?["opencliSource"]?.GetValue<string>()
            ?? metadata["opencliSource"]?.GetValue<string>()
            ?? openCliStep?["artifactSource"]?.GetValue<string>();

    private static bool HasDerivedArtifacts(string repositoryRoot, JsonObject? artifacts)
        => HasArtifactPath(repositoryRoot, artifacts, "crawlPath")
            || HasArtifactPath(repositoryRoot, artifacts, "xmldocPath");

    private static bool HasArtifactPath(string repositoryRoot, JsonObject? artifacts, string propertyName)
    {
        var relativePath = artifacts?[propertyName]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var absolutePath = Path.Combine(repositoryRoot, relativePath);
        return File.Exists(absolutePath);
    }
}

internal static class StoredOpenCliArtifactRegenerationSupport
{
    private const long MaxArtifactSizeBytes = 2 * 1024 * 1024;

    public static bool ProcessCandidate(string repositoryRoot, StoredOpenCliArtifactCandidate candidate)
    {
        var fileSize = new FileInfo(candidate.OpenCliPath).Length;
        if (fileSize > MaxArtifactSizeBytes)
        {
            var rejectedMetadataChanged = OpenCliArtifactRejectionSupport.RejectInvalidArtifact(
                repositoryRoot,
                candidate.MetadataPath,
                candidate.OpenCliPath,
                $"OpenCLI artifact is implausibly large ({fileSize / 1024 / 1024} MB).",
                xmldocPath: candidate.XmlDocPath);
            var rejectedStateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
            return rejectedMetadataChanged || rejectedStateChanged;
        }

        var existingNode = JsonNodeFileLoader.TryLoadJsonNode(candidate.OpenCliPath);
        if (existingNode is not JsonObject existing)
        {
            var rejectedMetadataChanged = OpenCliArtifactRejectionSupport.RejectInvalidArtifact(
                repositoryRoot,
                candidate.MetadataPath,
                candidate.OpenCliPath,
                "Stored OpenCLI artifact is not a JSON object.",
                xmldocPath: candidate.XmlDocPath);
            var rejectedStateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
            return rejectedMetadataChanged || rejectedStateChanged;
        }

        var sanitized = existing.DeepClone()?.AsObject()
            ?? throw new InvalidOperationException($"OpenCLI artifact '{candidate.OpenCliPath}' could not be cloned.");
        OpenCliDocumentSanitizer.EnsureArtifactSource(sanitized, candidate.ArtifactSource);
        OpenCliDocumentSanitizer.Sanitize(sanitized);

        if (!OpenCliDocumentValidator.TryValidateDocument(sanitized, out var validationError))
        {
            var rejectedMetadataChanged = OpenCliArtifactRejectionSupport.RejectInvalidArtifact(
                repositoryRoot,
                candidate.MetadataPath,
                candidate.OpenCliPath,
                validationError ?? "Stored OpenCLI artifact is not publishable.",
                xmldocPath: candidate.XmlDocPath);
            var rejectedStateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
            return rejectedMetadataChanged || rejectedStateChanged;
        }

        var openCliChanged = !JsonNode.DeepEquals(existing, sanitized);
        if (openCliChanged)
        {
            RepositoryPathResolver.WriteJsonFile(candidate.OpenCliPath, sanitized);
        }

        var metadataChanged = OpenCliArtifactMetadataRepair.SyncMetadata(
            repositoryRoot,
            candidate.MetadataPath,
            candidate.OpenCliPath,
            candidate.ArtifactSource,
            xmldocPath: candidate.XmlDocPath);
        var stateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
        return openCliChanged || metadataChanged || stateChanged;
    }
}
