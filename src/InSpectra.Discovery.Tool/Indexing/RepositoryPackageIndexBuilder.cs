using System.Text.Json.Nodes;

internal static class RepositoryPackageIndexBuilder
{
    public static RepositoryPackageIndexBuildResult Rebuild(string repositoryRoot, bool writeBrowserIndex)
    {
        var indexRoot = Path.Combine(repositoryRoot, "index");
        var packagesRoot = Path.Combine(indexRoot, "packages");
        Directory.CreateDirectory(indexRoot);
        Directory.CreateDirectory(packagesRoot);

        var versionRecords = Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase))
            .Select(path => new PackageRecord(
                JsonNode.Parse(File.ReadAllText(path))?.AsObject(),
                Path.GetDirectoryName(path)!))
            .Where(record => record.Metadata is not null)
            .Select(record => record!)
            .ToList();

        var currentSnapshotLookup = LoadCurrentPackageSnapshotLookup(repositoryRoot);
        var unsortedPackageSummaries = new List<JsonObject>();
        foreach (var packageGroup in versionRecords.GroupBy(record => record.Metadata!["packageId"]?.GetValue<string>() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(packageGroup.Key))
            {
                continue;
            }

            var orderedRecords = packageGroup
                .OrderByDescending(record => ParseDateTime(record.Metadata!["publishedAt"]?.GetValue<string>()))
                .ThenByDescending(record => ParseDateTime(record.Metadata!["evaluatedAt"]?.GetValue<string>()))
                .ToList();
            var latestRecord = orderedRecords[0];
            var lowerId = packageGroup.Key.ToLowerInvariant();
            var summaryPath = Path.Combine(packagesRoot, lowerId, "index.json");
            var existingSummary = File.Exists(summaryPath)
                ? JsonNode.Parse(File.ReadAllText(summaryPath))?.AsObject()
                : null;
            var summary = BuildPackageSummary(
                packageGroup.Select(record => record.Metadata!).ToList(),
                currentSnapshotLookup,
                existingSummary);
            SyncLatestDirectory(repositoryRoot, latestRecord.VersionDirectory, Path.Combine(packagesRoot, lowerId, "latest"));
            RepositoryPathResolver.WriteJsonFile(summaryPath, summary);
            unsortedPackageSummaries.Add(summary);
        }

        var packageSummaries = OpenCliMetrics.SortPackageSummariesForAllIndex(unsortedPackageSummaries, repositoryRoot);
        var now = DateTimeOffset.UtcNow;
        var allIndexTimestamps = ResolveDocumentTimestamps(Path.Combine(indexRoot, "all.json"), now);
        var allIndex = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["createdAt"] = allIndexTimestamps.CreatedAt,
            ["updatedAt"] = allIndexTimestamps.UpdatedAt,
            ["generatedAt"] = now.ToString("O"),
            ["packageCount"] = packageSummaries.Count,
            ["packages"] = new JsonArray(packageSummaries.Select(summary => (JsonNode)summary).ToArray()),
        };

        var allIndexPath = Path.Combine(indexRoot, "all.json");
        RepositoryPathResolver.WriteJsonFile(allIndexPath, allIndex);

        string? browserIndexPath = null;
        if (writeBrowserIndex)
        {
            browserIndexPath = Path.Combine(indexRoot, "index.json");
            WriteBrowserIndex(allIndex, browserIndexPath, now);
        }

        return new RepositoryPackageIndexBuildResult(packageSummaries.Count, allIndexPath, browserIndexPath);
    }

    public static string? ToIsoTimestamp(JsonNode? value)
    {
        var text = value?.GetValue<string>();
        return string.IsNullOrWhiteSpace(text) ? null : ParseDateTime(text).ToUniversalTime().ToString("O");
    }

    public static PackageEntryTimestamps ResolvePackageTimestamps(JsonObject package)
    {
        var timestamps = package["versions"]?.AsArray().OfType<JsonObject>()
            .Select(ResolveVersionTimestamp)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToList()
            ?? [];

        if (timestamps.Count == 0)
        {
            return new PackageEntryTimestamps(null, null);
        }

        return new PackageEntryTimestamps(
            timestamps[0].ToString("O"),
            timestamps[^1].ToString("O"));
    }

    private static JsonObject BuildPackageSummary(
        IReadOnlyList<JsonObject> records,
        IReadOnlyDictionary<string, CurrentPackageSnapshot> currentSnapshotLookup,
        JsonObject? existingSummary)
    {
        var ordered = records
            .OrderByDescending(record => ParseDateTime(record["publishedAt"]?.GetValue<string>()))
            .ThenByDescending(record => ParseDateTime(record["evaluatedAt"]?.GetValue<string>()))
            .ToList();
        var latest = ordered[0];
        var packageId = latest["packageId"]?.GetValue<string>();
        var lowerId = (packageId ?? string.Empty).ToLowerInvariant();
        var totalDownloads = ResolvePackageTotalDownloads(packageId, ordered, currentSnapshotLookup, existingSummary);
        var projectUrl = ResolvePackageLink(packageId, ordered, currentSnapshotLookup, existingSummary, "projectUrl");
        var sourceRepositoryUrl = ResolvePackageSourceRepositoryUrl(packageId, ordered, currentSnapshotLookup, existingSummary);

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
            ["latestPaths"] = new JsonObject
            {
                ["metadataPath"] = $"index/packages/{lowerId}/latest/metadata.json",
                ["opencliPath"] = latest["artifacts"]?["opencliPath"]?.GetValue<string>() is { Length: > 0 } ? $"index/packages/{lowerId}/latest/opencli.json" : null,
                ["crawlPath"] = latest["artifacts"]?["crawlPath"]?.GetValue<string>() is { Length: > 0 } ? $"index/packages/{lowerId}/latest/crawl.json" : null,
                ["xmldocPath"] = latest["artifacts"]?["xmldocPath"]?.GetValue<string>() is { Length: > 0 } ? $"index/packages/{lowerId}/latest/xmldoc.xml" : null,
            },
        };

        SetOptionalString(summary, "cliFramework", latest["cliFramework"]?.GetValue<string>());
        summary["versions"] = new JsonArray(ordered.Select(record => (JsonNode)BuildVersionRecord(record)).ToArray());
        return summary;
    }

    private static void SyncLatestDirectory(string repositoryRoot, string versionDirectory, string latestDirectory)
    {
        Directory.CreateDirectory(latestDirectory);
        foreach (var artifactName in new[] { "metadata.json", "opencli.json", "xmldoc.xml", "crawl.json" })
        {
            var sourcePath = Path.Combine(versionDirectory, artifactName);
            var targetPath = Path.Combine(latestDirectory, artifactName);
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
            }
            else if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        RebaseLatestMetadataPaths(repositoryRoot, latestDirectory);
    }

    private static void RebaseLatestMetadataPaths(string repositoryRoot, string latestDirectory)
    {
        var metadataPath = Path.Combine(latestDirectory, "metadata.json");
        if (!File.Exists(metadataPath))
        {
            return;
        }

        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))?.AsObject();
        if (metadata is null)
        {
            return;
        }

        var original = metadata.DeepClone();
        var artifacts = metadata["artifacts"] as JsonObject ?? new JsonObject();
        artifacts["metadataPath"] = RepositoryPathResolver.GetRelativePath(repositoryRoot, metadataPath);
        artifacts["opencliPath"] = ResolveLatestArtifactPath(repositoryRoot, latestDirectory, "opencli.json");
        artifacts["xmldocPath"] = ResolveLatestArtifactPath(repositoryRoot, latestDirectory, "xmldoc.xml");
        artifacts["crawlPath"] = ResolveLatestArtifactPath(repositoryRoot, latestDirectory, "crawl.json");
        metadata["artifacts"] = artifacts;

        if (metadata["steps"] is JsonObject steps)
        {
            if (steps["opencli"] is JsonObject openCliStep)
            {
                var openCliPath = ResolveLatestArtifactPath(repositoryRoot, latestDirectory, "opencli.json");
                if (openCliPath is null)
                {
                    openCliStep.Remove("path");
                }
                else
                {
                    openCliStep["path"] = openCliPath;
                }
            }

            if (steps["xmldoc"] is JsonObject xmlDocStep)
            {
                var xmlDocPath = ResolveLatestArtifactPath(repositoryRoot, latestDirectory, "xmldoc.xml");
                if (xmlDocPath is null)
                {
                    xmlDocStep.Remove("path");
                }
                else
                {
                    xmlDocStep["path"] = xmlDocPath;
                }
            }
        }

        if (!JsonNode.DeepEquals(original, metadata))
        {
            RepositoryPathResolver.WriteJsonFile(metadataPath, metadata);
        }
    }

    private static string? ResolveLatestArtifactPath(string repositoryRoot, string latestDirectory, string artifactName)
    {
        var artifactPath = Path.Combine(latestDirectory, artifactName);
        return File.Exists(artifactPath)
            ? RepositoryPathResolver.GetRelativePath(repositoryRoot, artifactPath)
            : null;
    }

    private static void WriteBrowserIndex(JsonObject allIndex, string outputPath, DateTimeOffset now)
    {
        var packages = new JsonArray();
        foreach (var package in allIndex["packages"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var latestVersionRecord = package["versions"]?.AsArray().OfType<JsonObject>().FirstOrDefault();
            var packageId = package["packageId"]?.GetValue<string>() ?? string.Empty;
            var latestVersion = package["latestVersion"]?.GetValue<string>() ?? string.Empty;
            var packageTimestamps = ResolvePackageTimestamps(package);
            var packageEntry = new JsonObject
            {
                ["packageId"] = packageId,
                ["commandName"] = latestVersionRecord?["command"]?.GetValue<string>(),
                ["versionCount"] = package["versions"]?.AsArray().Count ?? 0,
                ["latestVersion"] = latestVersion,
                ["createdAt"] = packageTimestamps.CreatedAt,
                ["updatedAt"] = packageTimestamps.UpdatedAt,
                ["completeness"] = package["latestStatus"]?.GetValue<string>() switch
                {
                    "ok" => "full",
                    "partial" => "partial",
                    var other => other,
                },
                ["packageIconUrl"] = string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(latestVersion)
                    ? null
                    : $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/{latestVersion.ToLowerInvariant()}/icon",
                ["totalDownloads"] = package["totalDownloads"]?.GetValue<long?>(),
                ["commandCount"] = package["commandCount"]?.GetValue<int?>() ?? 0,
                ["commandGroupCount"] = package["commandGroupCount"]?.GetValue<int?>() ?? 0,
            };
            SetOptionalString(packageEntry, "cliFramework", package["cliFramework"]?.GetValue<string>());
            packages.Add(packageEntry);
        }

        var timestamps = ResolveDocumentTimestamps(outputPath, now);
        RepositoryPathResolver.WriteJsonFile(outputPath, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["createdAt"] = timestamps.CreatedAt,
            ["updatedAt"] = timestamps.UpdatedAt,
            ["generatedAt"] = now.ToString("O"),
            ["packageCount"] = packages.Count,
            ["packages"] = packages,
        });
    }

    private static DocumentTimestamps ResolveDocumentTimestamps(string path, DateTimeOffset now)
    {
        var existing = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            : null;
        var createdAt = ToIsoTimestamp(existing?["createdAt"])
            ?? ToIsoTimestamp(existing?["generatedAt"])
            ?? now.ToString("O");

        return new DocumentTimestamps(createdAt, now.ToString("O"));
    }

    private static DateTimeOffset? ResolveVersionTimestamp(JsonObject version)
    {
        if (TryParseMeaningfulDateTime(version["publishedAt"]?.GetValue<string>(), out var publishedAt))
        {
            return publishedAt;
        }

        if (TryParseMeaningfulDateTime(version["evaluatedAt"]?.GetValue<string>(), out var evaluatedAt))
        {
            return evaluatedAt;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, CurrentPackageSnapshot> LoadCurrentPackageSnapshotLookup(string repositoryRoot)
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

    private static long? ResolvePackageTotalDownloads(
        string? packageId,
        IReadOnlyList<JsonObject> orderedRecords,
        IReadOnlyDictionary<string, CurrentPackageSnapshot> currentSnapshotLookup,
        JsonObject? existingSummary)
    {
        if (!string.IsNullOrWhiteSpace(packageId) &&
            currentSnapshotLookup.TryGetValue(packageId, out var latestSnapshot) &&
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
        string? packageId,
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

        if (!string.IsNullOrWhiteSpace(packageId) &&
            currentSnapshotLookup.TryGetValue(packageId, out var snapshot) &&
            string.Equals(propertyName, "projectUrl", StringComparison.Ordinal))
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
        string? packageId,
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

    private static string? BuildNuGetPackageUrl(string? packageId)
        => string.IsNullOrWhiteSpace(packageId)
            ? null
            : $"https://www.nuget.org/packages/{Uri.EscapeDataString(packageId)}";

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

    private static DateTimeOffset ParseDateTime(string? value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;

    private static bool TryParseDateTime(string? value, out DateTimeOffset parsed)
    {
        if (DateTimeOffset.TryParse(value, out parsed))
        {
            parsed = parsed.ToUniversalTime();
            return true;
        }

        return false;
    }

    private static bool TryParseMeaningfulDateTime(string? value, out DateTimeOffset parsed)
    {
        if (TryParseDateTime(value, out parsed) && parsed.Year > 1900)
        {
            return true;
        }

        parsed = default;
        return false;
    }

    private static JsonObject BuildVersionRecord(JsonObject record)
    {
        var versionRecord = new JsonObject
        {
            ["version"] = record["version"]?.GetValue<string>(),
            ["publishedAt"] = ToIsoTimestamp(record["publishedAt"]),
            ["evaluatedAt"] = ToIsoTimestamp(record["evaluatedAt"]),
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

    private sealed record DocumentTimestamps(string CreatedAt, string UpdatedAt);
    internal sealed record PackageEntryTimestamps(string? CreatedAt, string? UpdatedAt);

    private sealed record PackageRecord(JsonObject? Metadata, string VersionDirectory);

    private sealed record CurrentPackageSnapshot(long? TotalDownloads, string? ProjectUrl);
}

internal sealed record RepositoryPackageIndexBuildResult(int PackageCount, string AllIndexPath, string? BrowserIndexPath);
