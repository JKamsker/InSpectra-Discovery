using System.Text.Json.Nodes;

internal static class OpenCliArtifactMetadataRepair
{
    public static bool SyncMetadata(
        string repositoryRoot,
        string metadataPath,
        string openCliPath,
        string artifactSource,
        string? crawlPath = null,
        string? xmldocPath = null,
        bool synthesizedArtifact = false)
    {
        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))?.AsObject()
            ?? throw new InvalidOperationException($"Metadata artifact '{metadataPath}' is empty.");
        var original = metadata.DeepClone();

        metadata["status"] = "ok";

        var artifacts = metadata["artifacts"] as JsonObject ?? new JsonObject();
        artifacts["metadataPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, metadataPath);
        artifacts["opencliPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, openCliPath);
        artifacts["opencliSource"] = artifactSource;
        if (!string.IsNullOrWhiteSpace(crawlPath))
        {
            artifacts["crawlPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, crawlPath);
        }

        if (!string.IsNullOrWhiteSpace(xmldocPath))
        {
            artifacts["xmldocPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, xmldocPath);
        }

        metadata["artifacts"] = artifacts;

        var steps = metadata["steps"] as JsonObject ?? new JsonObject();
        var openCliStep = steps["opencli"] as JsonObject ?? new JsonObject();
        openCliStep["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, openCliPath);
        openCliStep["artifactSource"] = artifactSource;
        steps["opencli"] = openCliStep;

        if (!string.IsNullOrWhiteSpace(xmldocPath))
        {
            var xmlDocStep = steps["xmldoc"] as JsonObject ?? new JsonObject();
            xmlDocStep["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, xmldocPath);
            steps["xmldoc"] = xmlDocStep;
        }

        metadata["steps"] = steps;

        var introspection = metadata["introspection"] as JsonObject ?? new JsonObject();
        var openCliIntrospection = introspection["opencli"] as JsonObject ?? new JsonObject();
        openCliIntrospection["artifactSource"] = artifactSource;
        if (synthesizedArtifact)
        {
            openCliIntrospection["synthesizedArtifact"] = true;
        }

        introspection["opencli"] = openCliIntrospection;
        metadata["introspection"] = introspection;

        if (JsonNode.DeepEquals(original, metadata))
        {
            return false;
        }

        RepositoryPathResolver.WriteJsonFile(metadataPath, metadata);
        return true;
    }
}
