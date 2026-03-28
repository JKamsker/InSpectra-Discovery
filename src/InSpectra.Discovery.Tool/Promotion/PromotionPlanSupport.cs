using System.Text.Json.Nodes;

internal static class PromotionPlanSupport
{
    public static async Task<MergedPromotionPlan> LoadMergedPlanAsync(string downloadDirectory, CancellationToken cancellationToken)
    {
        var expectedPaths = Directory.GetFiles(downloadDirectory, "expected.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (expectedPaths.Length == 0)
        {
            throw new InvalidOperationException($"expected.json was not found under '{downloadDirectory}'.");
        }

        var itemsByKey = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        string? targetBranch = null;
        string? batchId = null;

        foreach (var expectedPath in expectedPaths)
        {
            var plan = JsonNode.Parse(await File.ReadAllTextAsync(expectedPath, cancellationToken))?.AsObject()
                ?? throw new InvalidOperationException($"Plan '{expectedPath}' is empty.");
            targetBranch ??= plan["targetBranch"]?.GetValue<string>();
            batchId ??= plan["batchId"]?.GetValue<string>();

            foreach (var item in plan["items"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var packageId = item["packageId"]?.GetValue<string>()
                    ?? throw new InvalidOperationException($"Plan '{expectedPath}' contains an item without packageId.");
                var version = item["version"]?.GetValue<string>()
                    ?? throw new InvalidOperationException($"Plan '{expectedPath}' contains an item without version.");
                var key = $"{packageId}|{version}";
                var cloned = item.DeepClone()?.AsObject() ?? new JsonObject();
                if (!itemsByKey.TryGetValue(key, out var existing) || GetAttempt(cloned) >= GetAttempt(existing))
                {
                    itemsByKey[key] = cloned;
                }
            }
        }

        var mergedItems = new JsonArray(itemsByKey.Values
            .OrderBy(item => item["packageId"]?.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item["version"]?.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
            .ToArray());
        var mergedBatchId = expectedPaths.Length == 1
            ? batchId
            : $"aggregate-{expectedPaths.Length}-plans";

        return new MergedPromotionPlan(mergedBatchId, targetBranch ?? "main", mergedItems);
    }

    private static int GetAttempt(JsonObject item)
        => item["attempt"]?.GetValue<int?>() ?? 0;
}

internal sealed record MergedPromotionPlan(
    string? BatchId,
    string TargetBranch,
    JsonArray Items);
