using System.Text.Json.Nodes;

internal sealed record HelpBatchPlan(string? BatchId, IReadOnlyList<HelpBatchItem> Items)
{
    public static HelpBatchPlan Load(string path)
    {
        var document = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidOperationException($"Plan '{path}' is empty.");
        var itemsNode = document["items"]?.AsArray()
            ?? throw new InvalidOperationException($"Plan '{path}' is missing an 'items' array.");

        var items = itemsNode.OfType<JsonObject>().Select(ParseItem).ToList();
        return new HelpBatchPlan(document["batchId"]?.GetValue<string>(), items);
    }

    private static HelpBatchItem ParseItem(JsonObject item)
        => new(
            PackageId: ReadRequiredString(item, "packageId"),
            Version: ReadRequiredString(item, "version"),
            CommandName: item["command"]?.GetValue<string>(),
            CliFramework: item["cliFramework"]?.GetValue<string>(),
            Attempt: item["attempt"]?.GetValue<int?>() ?? 1,
            ArtifactName: item["artifactName"]?.GetValue<string>(),
            PackageUrl: item["packageUrl"]?.GetValue<string>(),
            PackageContentUrl: item["packageContentUrl"]?.GetValue<string>(),
            CatalogEntryUrl: item["catalogEntryUrl"]?.GetValue<string>(),
            TotalDownloads: item["totalDownloads"]?.GetValue<long?>());

    private static string ReadRequiredString(JsonObject item, string propertyName)
        => item[propertyName]?.GetValue<string>() is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Plan item is missing required property '{propertyName}'.");
}

internal sealed record HelpBatchItem(
    string PackageId,
    string Version,
    string? CommandName,
    string? CliFramework,
    int Attempt,
    string? ArtifactName,
    string? PackageUrl,
    string? PackageContentUrl,
    string? CatalogEntryUrl,
    long? TotalDownloads);

internal sealed record HelpBatchTimeouts(
    int InstallTimeoutSeconds,
    int AnalysisTimeoutSeconds,
    int CommandTimeoutSeconds);
