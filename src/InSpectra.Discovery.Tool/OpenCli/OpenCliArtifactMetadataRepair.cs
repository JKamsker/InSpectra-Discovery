namespace InSpectra.Discovery.Tool.OpenCli;

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
        else
        {
            artifacts.Remove("crawlPath");
        }

        if (!string.IsNullOrWhiteSpace(xmldocPath))
        {
            artifacts["xmldocPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, xmldocPath);
        }
        else
        {
            artifacts.Remove("xmldocPath");
        }

        metadata["artifacts"] = artifacts;

        var steps = metadata["steps"] as JsonObject ?? new JsonObject();
        var openCliStep = steps["opencli"] as JsonObject ?? new JsonObject();
        openCliStep["status"] = "ok";
        openCliStep["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, openCliPath);
        openCliStep["artifactSource"] = artifactSource;
        openCliStep.Remove("message");
        var classification = ResolveClassification(
            artifactSource,
            OpenCliArtifactSourceSupport.InferClassification(artifactSource),
            openCliStep["classification"]?.GetValue<string>());
        if (!string.IsNullOrWhiteSpace(classification))
        {
            openCliStep["classification"] = classification;
        }

        steps["opencli"] = openCliStep;

        if (!string.IsNullOrWhiteSpace(xmldocPath))
        {
            var xmlDocStep = steps["xmldoc"] as JsonObject ?? new JsonObject();
            xmlDocStep["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, xmldocPath);
            steps["xmldoc"] = xmlDocStep;
        }
        else if (steps["xmldoc"] is JsonObject xmlDocStep)
        {
            xmlDocStep.Remove("path");
        }

        metadata["steps"] = steps;

        var introspection = metadata["introspection"] as JsonObject ?? new JsonObject();
        var openCliIntrospection = introspection["opencli"] as JsonObject ?? new JsonObject();
        openCliIntrospection["status"] = "ok";
        openCliIntrospection["artifactSource"] = artifactSource;
        openCliIntrospection.Remove("message");
        classification = ResolveClassification(
            artifactSource,
            classification,
            openCliIntrospection["classification"]?.GetValue<string>());
        if (!string.IsNullOrWhiteSpace(classification))
        {
            openCliIntrospection["classification"] = classification;
        }

        if (synthesizedArtifact)
        {
            openCliIntrospection["synthesizedArtifact"] = true;
        }
        else
        {
            openCliIntrospection.Remove("synthesizedArtifact");
        }

        introspection["opencli"] = openCliIntrospection;
        metadata["introspection"] = introspection;

        var analysisMode = OpenCliArtifactSourceSupport.InferAnalysisMode(artifactSource);
        if (!string.IsNullOrWhiteSpace(analysisMode))
        {
            metadata["analysisMode"] = analysisMode;

            var analysisSelection = metadata["analysisSelection"] as JsonObject ?? new JsonObject();
            analysisSelection["selectedMode"] = analysisMode;
            if (analysisSelection["preferredMode"] is null)
            {
                analysisSelection["preferredMode"] = analysisMode;
            }

            metadata["analysisSelection"] = analysisSelection;
        }

        if (JsonNode.DeepEquals(original, metadata))
        {
            return false;
        }

        RepositoryPathResolver.WriteJsonFile(metadataPath, metadata);
        return true;
    }

    private static string? ResolveClassification(
        string artifactSource,
        string? inferredClassification,
        string? existingClassification)
    {
        if (string.Equals(artifactSource, "tool-output", StringComparison.OrdinalIgnoreCase)
            && string.Equals(existingClassification, "json-ready-with-nonzero-exit", StringComparison.OrdinalIgnoreCase))
        {
            return existingClassification;
        }

        return !string.IsNullOrWhiteSpace(inferredClassification) ? inferredClassification : existingClassification;
    }
}

