namespace InSpectra.Discovery.Tool.OpenCli.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;

internal static class OpenCliArtifactRejectionSupport
{
    public static bool RejectInvalidArtifact(
        string repositoryRoot,
        string metadataPath,
        string openCliPath,
        string message,
        string? crawlPath = null,
        string? xmldocPath = null)
    {
        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))?.AsObject()
            ?? throw new InvalidOperationException($"Metadata artifact '{metadataPath}' is empty.");
        var original = metadata.DeepClone();

        var deletedOpenCli = false;
        if (File.Exists(openCliPath))
        {
            File.Delete(openCliPath);
            deletedOpenCli = true;
        }

        var artifacts = metadata["artifacts"] as JsonObject ?? new JsonObject();
        artifacts["metadataPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, metadataPath);
        artifacts.Remove("opencliPath");
        artifacts.Remove("opencliSource");
        SetOptionalRelativePath(artifacts, "crawlPath", repositoryRoot, crawlPath);
        SetOptionalRelativePath(artifacts, "xmldocPath", repositoryRoot, xmldocPath);
        metadata["artifacts"] = artifacts;

        if (!string.Equals(metadata["status"]?.GetValue<string>(), "partial", StringComparison.OrdinalIgnoreCase))
        {
            metadata["status"] = "partial";
        }

        var steps = metadata["steps"] as JsonObject ?? new JsonObject();
        var openCliStep = steps["opencli"] as JsonObject ?? new JsonObject();
        var analysisMode = ResolveAnalysisMode(metadata, artifacts, openCliStep);
        openCliStep["status"] = "failed";
        openCliStep["classification"] = "invalid-opencli-artifact";
        openCliStep["message"] = message;
        if (!string.IsNullOrWhiteSpace(analysisMode))
        {
            openCliStep["analysisMode"] = analysisMode;
        }
        openCliStep.Remove("path");
        openCliStep.Remove("artifactSource");
        steps["opencli"] = openCliStep;

        if (steps["xmldoc"] is JsonObject xmlDocStep)
        {
            if (HasExistingPath(xmldocPath))
            {
                xmlDocStep["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, xmldocPath!);
            }
            else
            {
                xmlDocStep.Remove("path");
            }
        }

        metadata["steps"] = steps;

        var introspection = metadata["introspection"] as JsonObject ?? new JsonObject();
        var openCliIntrospection = introspection["opencli"] as JsonObject ?? new JsonObject();
        openCliIntrospection["status"] = "invalid-output";
        openCliIntrospection["classification"] = "invalid-opencli-artifact";
        openCliIntrospection["message"] = message;
        openCliIntrospection.Remove("artifactSource");
        openCliIntrospection.Remove("synthesizedArtifact");
        introspection["opencli"] = openCliIntrospection;
        metadata["introspection"] = introspection;

        var metadataChanged = !JsonNode.DeepEquals(original, metadata);
        if (metadataChanged)
        {
            RepositoryPathResolver.WriteJsonFile(metadataPath, metadata);
        }

        var latestChanged = LatestArtifactRefreshSupport.SyncLatestDirectoryForVersion(repositoryRoot, metadataPath);
        return deletedOpenCli || metadataChanged || latestChanged;
    }

    private static void SetOptionalRelativePath(JsonObject target, string propertyName, string repositoryRoot, string? path)
    {
        if (HasExistingPath(path))
        {
            target[propertyName] = RepositoryPathResolver.GetRelativePath(repositoryRoot, path!);
        }
        else
        {
            target.Remove(propertyName);
        }
    }

    private static bool HasExistingPath(string? path)
        => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    private static string? ResolveAnalysisMode(JsonObject metadata, JsonObject? artifacts, JsonObject? openCliStep)
        => openCliStep?["analysisMode"]?.GetValue<string>()
            ?? metadata["analysisMode"]?.GetValue<string>()
            ?? metadata["analysisSelection"]?["selectedMode"]?.GetValue<string>()
            ?? OpenCliArtifactSourceSupport.InferAnalysisMode(
                openCliStep?["artifactSource"]?.GetValue<string>()
                ?? artifacts?["opencliSource"]?.GetValue<string>()
                ?? metadata["opencliSource"]?.GetValue<string>())
            ?? OpenCliArtifactSourceSupport.InferAnalysisModeFromClassification(
                openCliStep?["classification"]?.GetValue<string>());
}

