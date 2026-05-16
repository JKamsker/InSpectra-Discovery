namespace InSpectra.Discovery.Tool.Docs.Indexing;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.Indexing;


using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed record BrowserIndexDocument(
    int SchemaVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset GeneratedAt,
    int PackageCount,
    int? IncludedPackageCount,
    IReadOnlyList<JsonObject> Packages);

internal static class DocsBrowserIndexSupport
{
    public const int MinBrowserIndexPackageLimit = 200;

    public static BrowserIndexDocument BuildBrowserIndex(
        JsonObject allIndex,
        string outputFile,
        CancellationToken cancellationToken,
        DateTimeOffset? nowOverride = null)
    {
        var packageNodes = allIndex["packages"]?.AsArray() ?? [];
        var packages = new List<JsonObject>();
        var now = nowOverride ?? DateTimeOffset.UtcNow;

        foreach (var packageNode in packageNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (packageNode is not JsonObject package)
            {
                continue;
            }

            packages.Add(CreatePackageEntry(package));
        }

        var createdAt = ResolveDocumentCreatedAt(
            outputFile,
            allIndex["createdAt"]?.GetValue<string>() ?? allIndex["generatedAt"]?.GetValue<string>(),
            now);

        return new BrowserIndexDocument(
            SchemaVersion: 1,
            CreatedAt: createdAt,
            UpdatedAt: now,
            GeneratedAt: now,
            PackageCount: packages.Count,
            IncludedPackageCount: null,
            Packages: packages);
    }

    public static BrowserIndexDocument BuildMinBrowserIndex(
        BrowserIndexDocument browserIndex,
        int packageLimit = MinBrowserIndexPackageLimit)
    {
        var topPackages = browserIndex.Packages
            .OrderByDescending(package => GetOptionalInt(package, "commandGroupCount"))
            .ThenByDescending(package => GetOptionalInt(package, "commandCount"))
            .ThenByDescending(package => GetOptionalLong(package, "totalDownloads"))
            .ThenBy(package => package["packageId"]?.GetValue<string>() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Take(packageLimit)
            .ToArray();

        return new BrowserIndexDocument(
            SchemaVersion: browserIndex.SchemaVersion,
            CreatedAt: browserIndex.CreatedAt,
            UpdatedAt: browserIndex.UpdatedAt,
            GeneratedAt: browserIndex.GeneratedAt,
            PackageCount: browserIndex.PackageCount,
            IncludedPackageCount: topPackages.Length,
            Packages: topPackages);
    }

    public static JsonObject ToJsonObject(BrowserIndexDocument document)
        => JsonSerializer.SerializeToNode(document, JsonOptions.RepositoryFiles)?.AsObject()
           ?? throw new InvalidOperationException("Browser index document could not be serialized.");

    public static JsonObject StabilizeVolatileTimestamps(string outputPath, BrowserIndexDocument document)
    {
        var json = ToJsonObject(document);
        JsonDocumentStabilitySupport.TryPreserveTopLevelProperties(
            json,
            JsonNodeFileLoader.TryLoadJsonObject(outputPath),
            "updatedAt",
            "generatedAt");
        return json;
    }

    private static JsonObject CreatePackageEntry(JsonObject package)
    {
        var latestVersionRecord = package["versions"]?.AsArray().FirstOrDefault() as JsonObject;
        var packageId = package["packageId"]?.GetValue<string>() ?? string.Empty;
        var latestVersion = package["latestVersion"]?.GetValue<string>() ?? string.Empty;
        var packageTimestamps = RepositoryPackageIndexBuilder.ResolvePackageTimestamps(package);
        var packageEntry = new JsonObject
        {
            ["packageId"] = packageId,
            ["commandName"] = latestVersionRecord?["command"]?.GetValue<string>(),
            ["versionCount"] = package["versions"]?.AsArray().Count ?? 0,
            ["latestVersion"] = latestVersion,
            ["createdAt"] = packageTimestamps.CreatedAt,
            ["updatedAt"] = packageTimestamps.UpdatedAt,
            ["completeness"] = GetCompletenessLabel(package["latestStatus"]?.GetValue<string>()),
            ["packageIconUrl"] = string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(latestVersion)
                ? null
                : $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/{latestVersion.ToLowerInvariant()}/icon",
            ["totalDownloads"] = package["totalDownloads"]?.GetValue<long?>(),
            ["commandCount"] = package["commandCount"]?.GetValue<int?>() ?? 0,
            ["commandGroupCount"] = package["commandGroupCount"]?.GetValue<int?>() ?? 0,
        };
        SetOptionalString(packageEntry, "cliFramework", package["cliFramework"]?.GetValue<string>());
        return packageEntry;
    }

    private static string GetCompletenessLabel(string? latestStatus)
        => latestStatus switch
        {
            "ok" => "full",
            "partial" => "partial",
            _ => latestStatus ?? string.Empty,
        };

    private static void SetOptionalString(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[propertyName] = value;
        }
    }

    private static int GetOptionalInt(JsonObject package, string propertyName)
        => package[propertyName]?.GetValue<int?>() ?? 0;

    private static long GetOptionalLong(JsonObject package, string propertyName)
        => package[propertyName]?.GetValue<long?>() ?? 0L;

    private static DateTimeOffset ResolveDocumentCreatedAt(string outputFile, string? fallback, DateTimeOffset now)
    {
        var existing = JsonNodeFileLoader.TryLoadJsonObject(outputFile);
        if (DateTimeOffset.TryParse(existing?["createdAt"]?.GetValue<string>(), out var parsedCreatedAt))
        {
            return parsedCreatedAt.ToUniversalTime();
        }

        if (DateTimeOffset.TryParse(existing?["generatedAt"]?.GetValue<string>(), out var parsedGeneratedAt))
        {
            return parsedGeneratedAt.ToUniversalTime();
        }

        if (DateTimeOffset.TryParse(fallback, out var parsedFallback))
        {
            return parsedFallback.ToUniversalTime();
        }

        return now;
    }
}
