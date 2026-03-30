namespace InSpectra.Discovery.Tool.Analysis.Help;

using System.Text.Json.Nodes;

internal static class HelpBatchResultSupport
{
    public static JsonObject CreateSkippedItem(HelpBatchItem item, string reason)
        => new()
        {
            ["packageId"] = item.PackageId,
            ["version"] = item.Version,
            ["analysisMode"] = item.AnalysisMode,
            ["reason"] = reason,
        };

    public static IReadOnlyDictionary<string, HelpBatchSnapshotItem> LoadCurrentSnapshotLookup(string repositoryRoot)
    {
        var snapshotPath = Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json");
        var snapshot = JsonNodeFileLoader.TryLoadJsonObject(snapshotPath);
        var packages = snapshot?["packages"]?.AsArray();
        if (packages is null)
        {
            return new Dictionary<string, HelpBatchSnapshotItem>(StringComparer.OrdinalIgnoreCase);
        }

        return packages
            .OfType<JsonObject>()
            .Select(package => new HelpBatchSnapshotItem(
                PackageId: package["packageId"]?.GetValue<string>() ?? string.Empty,
                TotalDownloads: package["totalDownloads"]?.GetValue<long?>(),
                PackageUrl: package["packageUrl"]?.GetValue<string>(),
                PackageContentUrl: package["packageContentUrl"]?.GetValue<string>(),
                CatalogEntryUrl: package["catalogEntryUrl"]?.GetValue<string>()))
            .Where(package => !string.IsNullOrWhiteSpace(package.PackageId))
            .ToDictionary(package => package.PackageId, StringComparer.OrdinalIgnoreCase);
    }

    public static HelpBatchItemOutcome CreateOutcome(
        HelpBatchItem item,
        string artifactName,
        string itemOutputRoot,
        int exitCode,
        IReadOnlyDictionary<string, HelpBatchSnapshotItem> snapshotLookup)
    {
        var result = JsonNodeFileLoader.TryLoadJsonObject(Path.Combine(itemOutputRoot, "result.json"));
        var openCliArtifactName = result?["artifacts"]?["opencliArtifact"]?.GetValue<string>();
        var crawlArtifactName = result?["artifacts"]?["crawlArtifact"]?.GetValue<string>();
        var openCliExists = HasUsableOpenCliArtifact(itemOutputRoot, openCliArtifactName);
        var crawlExists = !HelpBatchArtifactSupport.RequiresCrawlArtifact(item.AnalysisMode)
            || HasUsableJsonArtifact(itemOutputRoot, crawlArtifactName);
        var disposition = result?["disposition"]?.GetValue<string>();
        var success = exitCode == 0
                      && string.Equals(disposition, "success", StringComparison.Ordinal)
                      && openCliExists
                      && crawlExists;
        var snapshot = snapshotLookup.TryGetValue(item.PackageId, out var value) ? value : null;

        return new HelpBatchItemOutcome(
            Success: success,
            FailureSummary: BuildFailureSummary(item, result, disposition, openCliExists, crawlExists),
            ExpectedItem: BuildExpectedItem(item, artifactName, result, snapshot));
    }

    private static JsonObject BuildExpectedItem(
        HelpBatchItem item,
        string artifactName,
        JsonObject? result,
        HelpBatchSnapshotItem? snapshot)
    {
        var expectedItem = new JsonObject
        {
            ["packageId"] = item.PackageId,
            ["version"] = item.Version,
            ["attempt"] = item.Attempt,
            ["command"] = item.CommandName,
            ["analysisMode"] = item.AnalysisMode,
            ["artifactName"] = artifactName,
            ["packageUrl"] = FirstNonEmpty(
                result?["packageUrl"]?.GetValue<string>(),
                item.PackageUrl,
                snapshot?.PackageUrl,
                $"https://www.nuget.org/packages/{item.PackageId}/{item.Version}"),
            ["packageContentUrl"] = FirstNonEmpty(
                result?["packageContentUrl"]?.GetValue<string>(),
                item.PackageContentUrl,
                snapshot?.PackageContentUrl),
            ["catalogEntryUrl"] = FirstNonEmpty(
                result?["catalogEntryUrl"]?.GetValue<string>(),
                item.CatalogEntryUrl,
                snapshot?.CatalogEntryUrl),
            ["totalDownloads"] = result?["totalDownloads"]?.GetValue<long?>() ?? item.TotalDownloads ?? snapshot?.TotalDownloads,
        };

        SetOptionalString(expectedItem, "cliFramework", item.CliFramework ?? result?["cliFramework"]?.GetValue<string>());
        return expectedItem;
    }

    private static string BuildFailureSummary(HelpBatchItem item, JsonObject? result, string? disposition, bool openCliExists, bool crawlExists)
    {
        var failureMessage = result?["failureMessage"]?.GetValue<string>();
        if (!string.Equals(disposition, "success", StringComparison.Ordinal))
        {
            return $"{item.PackageId} {item.Version}: {disposition ?? "missing-result"} {failureMessage ?? "No failure message was recorded."}";
        }

        if (!openCliExists)
        {
            return $"{item.PackageId} {item.Version}: success result is missing a usable opencli artifact.";
        }

        if (!crawlExists)
        {
            return $"{item.PackageId} {item.Version}: success result is missing a usable crawl artifact.";
        }

        return $"{item.PackageId} {item.Version}: analysis runner did not report success.";
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool HasUsableJsonArtifact(string artifactDirectory, string? artifactName)
    {
        var artifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, artifactName);
        return artifactPath is not null && PromotionArtifactSupport.TryLoadJsonObject(artifactPath, out _);
    }

    private static bool HasUsableOpenCliArtifact(string artifactDirectory, string? artifactName)
    {
        var artifactPath = PromotionArtifactSupport.ResolveOptionalArtifactPath(artifactDirectory, artifactName);
        return artifactPath is not null && OpenCliDocumentValidator.TryLoadValidDocument(artifactPath, out _, out _);
    }

    private static void SetOptionalString(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[propertyName] = value;
        }
    }
}

internal sealed record HelpBatchItemOutcome(bool Success, string FailureSummary, JsonObject ExpectedItem);
internal sealed record HelpBatchSnapshotItem(string PackageId, long? TotalDownloads, string? PackageUrl, string? PackageContentUrl, string? CatalogEntryUrl);
