namespace InSpectra.Discovery.Tool.Docs.Services;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;

internal static class LatestPartialMetadataPlanSupport
{
    public static void WriteExpectedPlan(
        string expectedPath,
        string batchId,
        IReadOnlyList<LatestPartialMetadataSelection> items,
        string targetBranch = "main")
    {
        var expected = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["batchId"] = batchId,
            ["targetBranch"] = string.IsNullOrWhiteSpace(targetBranch) ? "main" : targetBranch,
            ["items"] = new JsonArray(items
                .Select(item => new JsonObject
                {
                    ["packageId"] = item.PackageId,
                    ["version"] = item.Version,
                    ["attempt"] = item.NextAttempt,
                })
                .ToArray()),
        };

        RepositoryPathResolver.WriteJsonFile(expectedPath, expected);
    }
}
