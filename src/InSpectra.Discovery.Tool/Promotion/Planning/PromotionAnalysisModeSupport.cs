namespace InSpectra.Discovery.Tool.Promotion.Planning;

using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static class PromotionAnalysisModeSupport
{
    public static string? ResolveAnalysisMode(
        JsonObject? openCliArtifact,
        JsonObject? crawlArtifact,
        JsonObject item,
        JsonObject result)
        => FirstNonEmpty(
            InferCrawlBackedModeFromCrawl(crawlArtifact),
            InferAnalysisModeFromOpenCli(openCliArtifact),
            OpenCliArtifactSourceSupport.InferAnalysisMode(result["artifacts"]?["opencliSource"]?.GetValue<string>()),
            crawlArtifact is not null ? PreferCrawlBackedMode(result["analysisMode"]?.GetValue<string>()) : null,
            crawlArtifact is not null ? PreferCrawlBackedMode(item["analysisMode"]?.GetValue<string>()) : null,
            result["analysisMode"]?.GetValue<string>(),
            item["analysisMode"]?.GetValue<string>(),
            InferAnalysisModeFromOpenCliClassification(
                result["steps"]?["opencli"] as JsonObject,
                result["introspection"]?["opencli"] as JsonObject),
            crawlArtifact is not null ? "help" : null);

    public static void BackfillAnalysisModeSelection(JsonObject result, string? analysisMode)
    {
        if (string.IsNullOrWhiteSpace(analysisMode))
        {
            return;
        }

        result["analysisMode"] = analysisMode;

        var analysisSelection = result["analysisSelection"] as JsonObject;
        if (analysisSelection is null)
        {
            analysisSelection = new JsonObject();
            result["analysisSelection"] = analysisSelection;
        }

        analysisSelection["selectedMode"] = analysisMode;
        if (analysisSelection["preferredMode"] is null)
        {
            analysisSelection["preferredMode"] = analysisMode;
        }
    }

    public static bool TryLoadXmlArtifact(string xmlDocArtifactPath, out string? validationError)
    {
        try
        {
            _ = XDocument.Parse(File.ReadAllText(xmlDocArtifactPath));
            validationError = null;
            return true;
        }
        catch (Exception ex)
        {
            validationError = ex.Message;
            return false;
        }
    }

    private static string? InferAnalysisModeFromOpenCli(JsonObject? openCliArtifact)
        => OpenCliArtifactSourceSupport.InferAnalysisMode(
            openCliArtifact?["x-inspectra"]?["artifactSource"]?.GetValue<string>());

    private static string? InferAnalysisModeFromOpenCliClassification(params JsonObject?[] sources)
        => sources
            .Select(source => OpenCliArtifactSourceSupport.InferAnalysisModeFromClassification(
                source?["classification"]?.GetValue<string>()))
            .FirstOrDefault(mode => !string.IsNullOrWhiteSpace(mode));

    private static string? InferCrawlBackedModeFromCrawl(JsonObject? crawlArtifact)
    {
        if (crawlArtifact is null || crawlArtifact["staticCommands"] is not JsonArray)
        {
            return null;
        }

        var coverageMode = crawlArtifact["coverage"]?["coverageMode"]?.GetValue<string>();
        if (coverageMode is not null && coverageMode.Contains("static", StringComparison.OrdinalIgnoreCase))
        {
            return "static";
        }

        return "clifx";
    }

    private static string? PreferCrawlBackedMode(string? analysisMode)
        => string.Equals(analysisMode, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(analysisMode, "clifx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(analysisMode, "static", StringComparison.OrdinalIgnoreCase)
                ? analysisMode
                : null;

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

