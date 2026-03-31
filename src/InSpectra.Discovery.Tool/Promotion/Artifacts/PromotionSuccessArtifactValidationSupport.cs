namespace InSpectra.Discovery.Tool.Promotion.Artifacts;

using InSpectra.Discovery.Tool.Promotion.Results;

using InSpectra.Discovery.Tool.Analysis.Help.Batch;

using InSpectra.Discovery.Tool.Promotion.Planning;

using InSpectra.Discovery.Tool.OpenCli.Documents;


using InSpectra.Discovery.Tool.Analysis.Help;
using System.Text.Json.Nodes;

internal static class PromotionSuccessArtifactValidationSupport
{
    public static PromotionSuccessArtifactValidationOutcome Validate(
        JsonObject item,
        JsonObject result,
        string? artifactDirectory,
        string batchId,
        DateTimeOffset now)
    {
        var openCliArtifact = result["artifacts"]?["opencliArtifact"]?.GetValue<string>();
        var crawlArtifact = result["artifacts"]?["crawlArtifact"]?.GetValue<string>();
        var xmlDocArtifact = result["artifacts"]?["xmldocArtifact"]?.GetValue<string>();
        var openCliArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, openCliArtifact);
        var crawlArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, crawlArtifact);
        var xmlDocArtifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, xmlDocArtifact);
        var hasOpenCliArtifact = openCliArtifactPath is not null;
        var hasCrawlArtifact = crawlArtifactPath is not null;
        var hasXmlDocArtifact = xmlDocArtifactPath is not null;
        string? openCliValidationError = null;
        string? xmlDocValidationError = null;
        JsonObject? openCliDocument = null;
        JsonObject? crawlDocument = null;
        var hasUsableOpenCli = openCliArtifactPath is not null
            && OpenCliDocumentValidator.TryLoadValidDocument(openCliArtifactPath, out openCliDocument, out openCliValidationError);
        var hasUsableCrawl = crawlArtifactPath is not null
            && PromotionArtifactSupport.TryLoadJsonObject(crawlArtifactPath, out crawlDocument);
        var hasUsableXmlDoc = xmlDocArtifactPath is not null
            && PromotionAnalysisModeSupport.TryLoadXmlArtifact(xmlDocArtifactPath, out xmlDocValidationError);
        var selectedAnalysisMode = PromotionAnalysisModeSupport.ResolveAnalysisMode(
            hasUsableOpenCli ? openCliDocument : null,
            hasUsableCrawl ? crawlDocument : null,
            item,
            result);
        PromotionAnalysisModeSupport.BackfillAnalysisModeSelection(result, selectedAnalysisMode);

        var requiresCrawlArtifact = HelpBatchArtifactSupport.RequiresCrawlArtifact(selectedAnalysisMode);
        var declaredMissing = GetDeclaredMissingArtifacts(
            openCliArtifact,
            crawlArtifact,
            xmlDocArtifact,
            hasOpenCliArtifact,
            hasCrawlArtifact,
            hasXmlDocArtifact);
        var invalidArtifacts = GetInvalidArtifacts(
            openCliArtifact,
            crawlArtifact,
            xmlDocArtifact,
            openCliArtifactPath,
            crawlArtifactPath,
            xmlDocArtifactPath,
            hasUsableOpenCli,
            hasUsableCrawl,
            hasUsableXmlDoc,
            requiresCrawlArtifact);

        if (declaredMissing.Count == 0
            && invalidArtifacts.Count == 0
            && (hasUsableOpenCli || hasUsableXmlDoc)
            && (!requiresCrawlArtifact || hasUsableCrawl))
        {
            return new PromotionSuccessArtifactValidationOutcome(result, artifactDirectory);
        }

        var message = declaredMissing.Count > 0
            ? "Success result declared artifact(s) that were not uploaded: " + string.Join(", ", declaredMissing)
            : invalidArtifacts.Count > 0
                ? !string.IsNullOrWhiteSpace(openCliValidationError)
                    ? openCliValidationError
                    : !string.IsNullOrWhiteSpace(xmlDocValidationError)
                        ? xmlDocValidationError
                        : "Success result declared JSON artifact(s) that are not usable JSON objects: " + string.Join(", ", invalidArtifacts)
            : requiresCrawlArtifact && !hasUsableCrawl
                ? "Success result did not include a usable crawl.json artifact."
                : "Success result did not include either opencli.json or xmldoc.xml.";

            return new PromotionSuccessArtifactValidationOutcome(
            PromotionFailureResultSupport.NewSyntheticFailureResult(
                item,
                result["attempt"]?.GetValue<int?>() ?? item["attempt"]?.GetValue<int?>() ?? 1,
                "missing-success-artifact",
                message,
                batchId,
                now),
            ArtifactDirectory: null);
    }

    private static List<string> GetDeclaredMissingArtifacts(
        string? openCliArtifact,
        string? crawlArtifact,
        string? xmlDocArtifact,
        bool hasOpenCliArtifact,
        bool hasCrawlArtifact,
        bool hasXmlDocArtifact)
    {
        var declaredMissing = new List<string>();
        AddMissingArtifact(declaredMissing, openCliArtifact, hasOpenCliArtifact);
        AddMissingArtifact(declaredMissing, crawlArtifact, hasCrawlArtifact);
        AddMissingArtifact(declaredMissing, xmlDocArtifact, hasXmlDocArtifact);
        return declaredMissing;
    }

    private static List<string> GetInvalidArtifacts(
        string? openCliArtifact,
        string? crawlArtifact,
        string? xmlDocArtifact,
        string? openCliArtifactPath,
        string? crawlArtifactPath,
        string? xmlDocArtifactPath,
        bool hasUsableOpenCli,
        bool hasUsableCrawl,
        bool hasUsableXmlDoc,
        bool requiresCrawlArtifact)
    {
        var invalidArtifacts = new List<string>();

        if (openCliArtifactPath is not null && !hasUsableOpenCli && (!hasUsableXmlDoc || requiresCrawlArtifact))
        {
            invalidArtifacts.Add(openCliArtifact!);
        }

        if (crawlArtifactPath is not null && !hasUsableCrawl)
        {
            invalidArtifacts.Add(crawlArtifact!);
        }

        if (xmlDocArtifactPath is not null && !hasUsableXmlDoc)
        {
            invalidArtifacts.Add(xmlDocArtifact!);
        }

        return invalidArtifacts;
    }

    private static void AddMissingArtifact(List<string> declaredMissing, string? artifactName, bool exists)
    {
        if (!string.IsNullOrWhiteSpace(artifactName) && !exists)
        {
            declaredMissing.Add(artifactName);
        }
    }
}

internal sealed record PromotionSuccessArtifactValidationOutcome(JsonObject Result, string? ArtifactDirectory);

