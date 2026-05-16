namespace InSpectra.Discovery.Tool.Promotion.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using InSpectra.Discovery.Tool.OpenCli.Documents;


using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static class PromotionOpenCliArtifactSupport
{
    public static async Task<PromotionPreparedOpenCliArtifact> PrepareAsync(
        JsonObject result,
        string packageId,
        string version,
        string? openCliArtifactPath,
        string? xmlDocArtifactPath,
        CancellationToken cancellationToken)
    {
        string? openCliSource = null;
        JsonObject? openCliDocument = null;
        string? xmlDocContent = null;

        if (openCliArtifactPath is not null
            && OpenCliDocumentValidator.TryLoadValidDocument(openCliArtifactPath, out var parsedOpenCli, out _))
        {
            openCliSource = ResolveOpenCliSource(parsedOpenCli!, result);
            OpenCliDocumentSanitizer.EnsureArtifactSource(parsedOpenCli!, openCliSource);
            openCliDocument = OpenCliDocumentSanitizer.Sanitize(parsedOpenCli!);
        }

        if (xmlDocArtifactPath is not null)
        {
            xmlDocContent = await File.ReadAllTextAsync(xmlDocArtifactPath, cancellationToken);
        }

        if (openCliDocument is null && !string.IsNullOrWhiteSpace(xmlDocContent))
        {
            openCliDocument = OpenCliDocumentSynthesizer.ConvertFromXmldoc(
                XDocument.Parse(xmlDocContent),
                result["command"]?.GetValue<string>() ?? packageId,
                version);
            openCliSource = "synthesized-from-xmldoc";
        }

        if (openCliDocument is not null && !string.IsNullOrWhiteSpace(result["cliFramework"]?.GetValue<string>()))
        {
            var inspectra = openCliDocument["x-inspectra"] as JsonObject ?? new JsonObject();
            openCliDocument["x-inspectra"] = inspectra;
            inspectra["cliFramework"] = result["cliFramework"]!.GetValue<string>();
        }

        return new PromotionPreparedOpenCliArtifact(openCliDocument, openCliSource, xmlDocContent);
    }

    public static string? ResolveMetadataAnalysisMode(JsonObject result, PromotionPreparedOpenCliArtifact preparedArtifact)
        => OpenCliArtifactSourceSupport.InferAnalysisMode(preparedArtifact.OpenCliSource)
            ?? OpenCliArtifactSourceSupport.InferAnalysisMode(preparedArtifact.OpenCliDocument?["x-inspectra"]?["artifactSource"]?.GetValue<string>())
            ?? result["analysisMode"]?.GetValue<string>();

    public static JsonObject? BuildMetadataAnalysisSelection(JsonObject result, string? metadataAnalysisMode)
    {
        var metadataAnalysisSelection = result["analysisSelection"]?.DeepClone() as JsonObject;
        if (string.IsNullOrWhiteSpace(metadataAnalysisMode))
        {
            return metadataAnalysisSelection;
        }

        metadataAnalysisSelection ??= new JsonObject();
        metadataAnalysisSelection["selectedMode"] = metadataAnalysisMode;
        if (metadataAnalysisSelection["preferredMode"] is null)
        {
            metadataAnalysisSelection["preferredMode"] = metadataAnalysisMode;
        }

        return metadataAnalysisSelection;
    }

    public static string? ResolveOpenCliClassification(string? openCliSource, params JsonObject?[] sources)
    {
        if (string.Equals(openCliSource, "tool-output", StringComparison.OrdinalIgnoreCase))
        {
            var preserved = sources
                .Select(source => source?["classification"]?.GetValue<string>())
                .FirstOrDefault(classification => string.Equals(classification, "json-ready-with-nonzero-exit", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(preserved))
            {
                return preserved;
            }
        }

        return OpenCliArtifactSourceSupport.InferClassification(openCliSource);
    }

    public static void BackfillOpenCliStepMetadata(
        JsonObject openCliStep,
        string repositoryRoot,
        string openCliPath,
        string? openCliSource,
        string? inferredOpenCliClassification)
    {
        openCliStep["status"] = "ok";
        openCliStep["path"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, openCliPath);
        openCliStep.Remove("message");
        if (!string.IsNullOrWhiteSpace(openCliSource))
        {
            openCliStep["artifactSource"] = openCliSource;
        }

        if (!string.IsNullOrWhiteSpace(inferredOpenCliClassification))
        {
            openCliStep["classification"] = inferredOpenCliClassification;
        }
    }

    public static void BackfillOpenCliIntrospectionMetadata(
        JsonObject openCliIntrospection,
        string? openCliSource,
        string? inferredOpenCliClassification)
    {
        openCliIntrospection["status"] = "ok";
        openCliIntrospection.Remove("message");
        if (string.Equals(openCliSource, "synthesized-from-xmldoc", StringComparison.Ordinal))
        {
            openCliIntrospection["synthesizedArtifact"] = true;
        }
        else
        {
            openCliIntrospection.Remove("synthesizedArtifact");
        }

        if (!string.IsNullOrWhiteSpace(openCliSource))
        {
            openCliIntrospection["artifactSource"] = openCliSource;
        }

        if (!string.IsNullOrWhiteSpace(inferredOpenCliClassification))
        {
            openCliIntrospection["classification"] = inferredOpenCliClassification;
        }
    }

    private static string ResolveOpenCliSource(JsonObject document, JsonObject result)
        => FirstNonEmpty(
            document["x-inspectra"]?["artifactSource"]?.GetValue<string>(),
            result["artifacts"]?["opencliSource"]?.GetValue<string>(),
            result["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>(),
            result["introspection"]?["opencli"]?["artifactSource"]?.GetValue<string>(),
            OpenCliArtifactSourceSupport.InferArtifactSource(result["analysisMode"]?.GetValue<string>()),
            "tool-output") ?? "tool-output";

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

internal sealed record PromotionPreparedOpenCliArtifact(
    JsonObject? OpenCliDocument,
    string? OpenCliSource,
    string? XmlDocContent)
{
    public bool HasOpenCliOutput => OpenCliDocument is not null;

    public bool HasXmlDocContent => !string.IsNullOrWhiteSpace(XmlDocContent);
}

