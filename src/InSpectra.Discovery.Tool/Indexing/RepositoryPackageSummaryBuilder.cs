namespace InSpectra.Discovery.Tool.Indexing;

using System.Text.Json.Nodes;

internal static class RepositoryPackageSummaryBuilder
{
    public static IReadOnlyDictionary<string, CurrentPackageSnapshot> LoadCurrentPackageSnapshotLookup(string repositoryRoot)
    {
        var snapshotPath = Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json");
        if (!File.Exists(snapshotPath))
        {
            return new Dictionary<string, CurrentPackageSnapshot>(StringComparer.OrdinalIgnoreCase);
        }

        var snapshot = JsonNode.Parse(File.ReadAllText(snapshotPath))?.AsObject();
        var packages = snapshot?["packages"]?.AsArray();
        if (packages is null)
        {
            return new Dictionary<string, CurrentPackageSnapshot>(StringComparer.OrdinalIgnoreCase);
        }

        var lookup = new Dictionary<string, CurrentPackageSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in packages.OfType<JsonObject>())
        {
            var packageId = package["packageId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(packageId))
            {
                continue;
            }

            lookup[packageId] = new CurrentPackageSnapshot(
                package["totalDownloads"]?.GetValue<long?>(),
                package["projectUrl"]?.GetValue<string>());
        }

        return lookup;
    }

    public static JsonObject BuildPackageSummary(
        string packageId,
        IReadOnlyList<JsonObject> orderedRecords,
        IReadOnlyDictionary<string, CurrentPackageSnapshot> currentSnapshotLookup,
        JsonObject? existingSummary)
    {
        var latest = orderedRecords[0];
        var totalDownloads = ResolvePackageTotalDownloads(packageId, orderedRecords, currentSnapshotLookup, existingSummary);
        var projectUrl = ResolvePackageLink(packageId, orderedRecords, currentSnapshotLookup, existingSummary, "projectUrl");
        var sourceRepositoryUrl = ResolvePackageSourceRepositoryUrl(packageId, orderedRecords, currentSnapshotLookup, existingSummary);

        var summary = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["trusted"] = latest["trusted"]?.GetValue<bool?>(),
            ["totalDownloads"] = totalDownloads,
            ["links"] = new JsonObject
            {
                ["nuget"] = BuildNuGetPackageUrl(packageId),
                ["project"] = projectUrl,
                ["source"] = sourceRepositoryUrl,
            },
            ["latestVersion"] = latest["version"]?.GetValue<string>(),
            ["latestStatus"] = latest["status"]?.GetValue<string>(),
            ["versions"] = new JsonArray(orderedRecords.Select(record => (JsonNode)BuildVersionRecord(record)).ToArray()),
        };

        SetOptionalString(summary, "cliFramework", latest["cliFramework"]?.GetValue<string>());
        return summary;
    }

    private static long? ResolvePackageTotalDownloads(
        string packageId,
        IReadOnlyList<JsonObject> orderedRecords,
        IReadOnlyDictionary<string, CurrentPackageSnapshot> currentSnapshotLookup,
        JsonObject? existingSummary)
    {
        if (currentSnapshotLookup.TryGetValue(packageId, out var latestSnapshot) &&
            latestSnapshot.TotalDownloads is not null)
        {
            return latestSnapshot.TotalDownloads.Value;
        }

        var historicalTotalDownloads = orderedRecords
            .Select(record => record["totalDownloads"]?.GetValue<long?>())
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();

        if (historicalTotalDownloads > 0 || orderedRecords.Any(record => record["totalDownloads"] is not null))
        {
            return historicalTotalDownloads;
        }

        return existingSummary?["totalDownloads"]?.GetValue<long?>();
    }

    private static string? ResolvePackageLink(
        string packageId,
        IReadOnlyList<JsonObject> orderedRecords,
        IReadOnlyDictionary<string, CurrentPackageSnapshot> currentSnapshotLookup,
        JsonObject? existingSummary,
        string propertyName)
    {
        foreach (var record in orderedRecords)
        {
            var value = NormalizeLinkUrl(record[propertyName]?.GetValue<string>());
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        if (string.Equals(propertyName, "projectUrl", StringComparison.Ordinal)
            && currentSnapshotLookup.TryGetValue(packageId, out var snapshot))
        {
            var projectUrl = NormalizeLinkUrl(snapshot.ProjectUrl);
            if (!string.IsNullOrWhiteSpace(projectUrl))
            {
                return projectUrl;
            }
        }

        return NormalizeLinkUrl(existingSummary?["links"]?[propertyName switch
        {
            "projectUrl" => "project",
            _ => propertyName,
        }]?.GetValue<string>());
    }

    private static string? ResolvePackageSourceRepositoryUrl(
        string packageId,
        IReadOnlyList<JsonObject> orderedRecords,
        IReadOnlyDictionary<string, CurrentPackageSnapshot> currentSnapshotLookup,
        JsonObject? existingSummary)
    {
        foreach (var record in orderedRecords)
        {
            var sourceRepositoryUrl = PackageVersionResolver.NormalizeRepositoryUrl(record["sourceRepositoryUrl"]?.GetValue<string>());
            if (!string.IsNullOrWhiteSpace(sourceRepositoryUrl))
            {
                return sourceRepositoryUrl;
            }
        }

        var projectUrl = ResolvePackageLink(packageId, orderedRecords, currentSnapshotLookup, existingSummary, "projectUrl");
        if (!string.IsNullOrWhiteSpace(projectUrl) && LooksLikeRepositoryUrl(projectUrl))
        {
            return projectUrl;
        }

        return PackageVersionResolver.NormalizeRepositoryUrl(existingSummary?["links"]?["source"]?.GetValue<string>());
    }

    private static string? BuildNuGetPackageUrl(string packageId)
        => $"https://www.nuget.org/packages/{Uri.EscapeDataString(packageId)}";

    private static string? NormalizeLinkUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return Uri.TryCreate(normalized, UriKind.Absolute, out _)
            ? normalized
            : null;
    }

    private static bool LooksLikeRepositoryUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith("gitlab.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith("bitbucket.org", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith("dev.azure.com", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject BuildVersionRecord(JsonObject record)
    {
        var versionRecord = new JsonObject
        {
            ["version"] = record["version"]?.GetValue<string>(),
            ["publishedAt"] = RepositoryPackageIndexTimestampSupport.ToIsoTimestamp(record["publishedAt"]),
            ["evaluatedAt"] = RepositoryPackageIndexTimestampSupport.ToIsoTimestamp(record["evaluatedAt"]),
            ["status"] = record["status"]?.GetValue<string>(),
            ["command"] = record["command"]?.GetValue<string>(),
            ["timings"] = record["timings"]?.DeepClone(),
            ["paths"] = record["artifacts"]?.DeepClone(),
        };

        SetOptionalString(versionRecord, "cliFramework", record["cliFramework"]?.GetValue<string>());
        return versionRecord;
    }

    private static void SetOptionalString(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[propertyName] = value;
        }
    }
}

internal sealed record CurrentPackageSnapshot(long? TotalDownloads, string? ProjectUrl);

