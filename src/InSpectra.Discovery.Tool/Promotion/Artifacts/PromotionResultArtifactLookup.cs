namespace InSpectra.Discovery.Tool.Promotion.Artifacts;

using InSpectra.Discovery.Tool.Analysis.Help.Batch;

using InSpectra.Discovery.Tool.Infrastructure.Json;


using InSpectra.Discovery.Tool.Analysis.Help;
using System.Text.Json.Nodes;

internal sealed class PromotionResultArtifactLookup
{
    private readonly Dictionary<string, PromotionResultArtifactEntry> _entries;
    private readonly Dictionary<string, PromotionResultArtifactEntry> _legacyEntries;

    private PromotionResultArtifactLookup(
        Dictionary<string, PromotionResultArtifactEntry> entries,
        Dictionary<string, PromotionResultArtifactEntry> legacyEntries)
    {
        _entries = entries;
        _legacyEntries = legacyEntries;
    }

    public static async Task<PromotionResultArtifactLookup> BuildAsync(string downloadDirectory, CancellationToken cancellationToken)
    {
        var entries = new Dictionary<string, PromotionResultArtifactEntry>(StringComparer.OrdinalIgnoreCase);
        var legacyEntries = new Dictionary<string, PromotionResultArtifactEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var resultPath in Directory.EnumerateFiles(downloadDirectory, "result.json", SearchOption.AllDirectories))
        {
            var result = await JsonNodeFileLoader.TryLoadJsonObjectAsync(resultPath, cancellationToken);
            if (result is null)
            {
                continue;
            }

            var artifactDirectory = Path.GetDirectoryName(resultPath)!;
            var entry = new PromotionResultArtifactEntry(result, artifactDirectory);

            foreach (var key in HelpBatchArtifactSupport.BuildResultKeys(result, artifactDirectory))
            {
                UpsertByAttempt(entries, key, entry);
            }

            var packageId = result["packageId"]?.GetValue<string>();
            var version = result["version"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            var legacyKey = HelpBatchArtifactSupport.BuildPackageVersionKey(packageId, version);
            UpsertByAttempt(legacyEntries, legacyKey, entry);
        }

        return new PromotionResultArtifactLookup(entries, legacyEntries);
    }

    public bool TryResolve(JsonObject item, out PromotionResultArtifactEntry? entry)
    {
        var key = HelpBatchArtifactSupport.BuildPlanItemKey(item);
        if (_entries.TryGetValue(key, out var resolvedEntry))
        {
            entry = resolvedEntry;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(item["artifactName"]?.GetValue<string>())
            || !string.IsNullOrWhiteSpace(item["command"]?.GetValue<string>()))
        {
            entry = null;
            return false;
        }

        var packageId = item["packageId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Plan item is missing packageId.");
        var version = item["version"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"Plan item '{packageId}' is missing version.");
        if (_legacyEntries.TryGetValue(HelpBatchArtifactSupport.BuildPackageVersionKey(packageId, version), out resolvedEntry))
        {
            entry = resolvedEntry;
            return true;
        }

        entry = null;
        return false;
    }

    private static void UpsertByAttempt(
        Dictionary<string, PromotionResultArtifactEntry> lookup,
        string key,
        PromotionResultArtifactEntry candidate)
    {
        if (!lookup.TryGetValue(key, out var existing)
            || GetAttempt(candidate.Result) >= GetAttempt(existing.Result))
        {
            lookup[key] = candidate;
        }
    }

    private static int GetAttempt(JsonObject result)
        => result["attempt"]?.GetValue<int?>() ?? 0;
}

internal sealed record PromotionResultArtifactEntry(JsonObject Result, string ArtifactDirectory);

