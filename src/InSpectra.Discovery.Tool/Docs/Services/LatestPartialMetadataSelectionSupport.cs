namespace InSpectra.Discovery.Tool.Docs.Services;

using InSpectra.Discovery.Tool.Infrastructure.Json;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;

internal sealed record LatestPartialMetadataSelection(
    string PackageId,
    string Version,
    int NextAttempt,
    string? AnalysisMode,
    string? Classification,
    string? Message);

internal sealed record LatestPartialMetadataSelectionCriteria(
    string? PackageId = null,
    string? Version = null,
    string? AnalysisMode = null,
    string? Classification = null,
    string? MessageContains = null,
    int? Limit = null);

internal static class LatestPartialMetadataSelectionSupport
{
    public static IReadOnlyList<LatestPartialMetadataSelection> Select(
        string repositoryRoot,
        LatestPartialMetadataSelectionCriteria criteria)
    {
        var packagesRoot = Path.Combine(repositoryRoot, "index", "packages");
        if (!Directory.Exists(packagesRoot))
        {
            return [];
        }

        var matches = new List<LatestPartialMetadataSelection>();
        foreach (var metadataPath in Directory.EnumerateFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories))
        {
            if (!IsLatestMetadataPath(metadataPath))
            {
                continue;
            }

            var metadata = JsonNodeFileLoader.TryLoadJsonObject(metadataPath);
            if (metadata is null
                || !string.Equals(metadata["status"]?.GetValue<string>(), "partial", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var packageId = metadata["packageId"]?.GetValue<string>();
            var version = metadata["version"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            var analysisMode = ResolveAnalysisMode(metadata);
            var classification = metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>();
            var message = metadata["steps"]?["opencli"]?["message"]?.GetValue<string>();
            if (!Matches(packageId, criteria.PackageId)
                || !Matches(version, criteria.Version)
                || !Matches(analysisMode, criteria.AnalysisMode)
                || !Matches(classification, criteria.Classification)
                || !Contains(message, criteria.MessageContains))
            {
                continue;
            }

            matches.Add(new LatestPartialMetadataSelection(
                packageId,
                version,
                NextAttempt: Math.Max(1, (metadata["attempt"]?.GetValue<int?>() ?? 0) + 1),
                AnalysisMode: analysisMode,
                Classification: classification,
                Message: message));
        }

        IEnumerable<LatestPartialMetadataSelection> ordered = matches
            .OrderBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Version, StringComparer.OrdinalIgnoreCase);
        if (criteria.Limit is > 0)
        {
            ordered = ordered.Take(criteria.Limit.Value);
        }

        return ordered.ToArray();
    }

    private static bool IsLatestMetadataPath(string metadataPath)
    {
        var directory = Path.GetDirectoryName(metadataPath);
        return string.Equals(
            Path.GetFileName(directory),
            "latest",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveAnalysisMode(JsonObject metadata)
    {
        var stepOpenCli = metadata["steps"]?["opencli"] as JsonObject;
        var introspectionOpenCli = metadata["introspection"]?["opencli"] as JsonObject;
        var artifacts = metadata["artifacts"] as JsonObject;

        return stepOpenCli?["analysisMode"]?.GetValue<string>()
            ?? metadata["analysisMode"]?.GetValue<string>()
            ?? metadata["analysisSelection"]?["selectedMode"]?.GetValue<string>()
            ?? OpenCliArtifactSourceSupport.InferAnalysisMode(
                stepOpenCli?["artifactSource"]?.GetValue<string>()
                ?? artifacts?["opencliSource"]?.GetValue<string>()
                ?? metadata["opencliSource"]?.GetValue<string>())
            ?? OpenCliArtifactSourceSupport.InferAnalysisModeFromClassification(
                stepOpenCli?["classification"]?.GetValue<string>()
                ?? introspectionOpenCli?["classification"]?.GetValue<string>());
    }

    private static bool Matches(string? value, string? filter)
        => string.IsNullOrWhiteSpace(filter)
            || string.Equals(value, filter, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? value, string? filter)
        => string.IsNullOrWhiteSpace(filter)
            || (!string.IsNullOrWhiteSpace(value)
                && value.Contains(filter, StringComparison.OrdinalIgnoreCase));
}
