namespace InSpectra.Discovery.Tool.Promotion.Planning;

using InSpectra.Discovery.Tool.Frameworks;

using System.Text.Json.Nodes;

internal static class PromotionPlanItemMergeSupport
{
    public static void MergeIntoResult(JsonObject item, JsonObject result)
    {
        if (result["totalDownloads"] is null && item["totalDownloads"] is not null)
        {
            result["totalDownloads"] = item["totalDownloads"]?.DeepClone();
        }

        if (result["command"] is null && item["command"] is not null)
        {
            result["command"] = item["command"]?.DeepClone();
        }

        if (CliFrameworkProviderRegistry.ShouldReplace(result["cliFramework"]?.GetValue<string>(), item["cliFramework"]?.GetValue<string>()))
        {
            result["cliFramework"] = item["cliFramework"]?.DeepClone();
        }
        else if (result["cliFramework"] is null && item["cliFramework"] is not null)
        {
            result["cliFramework"] = item["cliFramework"]?.DeepClone();
        }

        if (item["analysisMode"] is not null)
        {
            var analysisMode = item["analysisMode"]?.DeepClone();
            if (result["analysisMode"] is null)
            {
                result["analysisMode"] = analysisMode;
            }

            var analysisSelection = result["analysisSelection"] as JsonObject;
            if (analysisSelection is null)
            {
                analysisSelection = new JsonObject();
                result["analysisSelection"] = analysisSelection;
            }

            if (analysisSelection["selectedMode"] is null)
            {
                analysisSelection["selectedMode"] = analysisMode?.DeepClone();
            }

            if (analysisSelection["preferredMode"] is null)
            {
                analysisSelection["preferredMode"] = analysisMode?.DeepClone();
            }
        }
    }
}

