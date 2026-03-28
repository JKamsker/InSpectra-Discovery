using System.Text.Json.Nodes;

internal static class HelpBatchArtifactSupport
{
    public static string ResolveArtifactName(HelpBatchItem item)
        => string.IsNullOrWhiteSpace(item.ArtifactName)
            ? BuildDefaultArtifactName(item.PackageId, item.Version, item.CommandName)
            : item.ArtifactName;

    public static string BuildPlanItemKey(JsonObject item)
        => BuildKey(
            item["packageId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Plan item is missing packageId."),
            item["version"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Plan item is missing version."),
            item["artifactName"]?.GetValue<string>(),
            item["command"]?.GetValue<string>());

    public static IReadOnlyList<string> BuildResultKeys(JsonObject result, string artifactDirectory)
    {
        var packageId = result["packageId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Result is missing packageId.");
        var version = result["version"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Result is missing version.");
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddKey(keys, packageId, version, "artifact", result["artifactName"]?.GetValue<string>());
        AddKey(keys, packageId, version, "artifact", Path.GetFileName(artifactDirectory));
        AddKey(keys, packageId, version, "command", result["command"]?.GetValue<string>());

        return keys.ToArray();
    }

    public static bool RequiresCrawlArtifact(string? analysisMode)
        => string.Equals(analysisMode, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(analysisMode, "clifx", StringComparison.OrdinalIgnoreCase);

    public static string BuildPackageVersionKey(string packageId, string version)
        => $"{packageId}|{version}";

    private static string BuildKey(string packageId, string version, string? artifactName, string? commandName)
    {
        var discriminator = !string.IsNullOrWhiteSpace(artifactName)
            ? $"artifact:{artifactName}"
            : !string.IsNullOrWhiteSpace(commandName)
                ? $"command:{commandName}"
                : string.Empty;
        return $"{packageId}|{version}|{discriminator}";
    }

    private static string BuildDefaultArtifactName(string packageId, string version, string? commandName)
    {
        var suffix = string.IsNullOrWhiteSpace(commandName)
            ? null
            : "-" + NormalizeSegment(commandName);
        return NormalizeSegment($"analysis-{packageId.ToLowerInvariant()}-{version.ToLowerInvariant()}{suffix}");
    }

    private static string NormalizeSegment(string value)
    {
        var normalized = new string(value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '-')
            .ToArray())
            .Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static void AddKey(HashSet<string> keys, string packageId, string version, string prefix, string? discriminator)
    {
        if (!string.IsNullOrWhiteSpace(discriminator))
        {
            keys.Add($"{packageId}|{version}|{prefix}:{discriminator}");
        }
    }
}
