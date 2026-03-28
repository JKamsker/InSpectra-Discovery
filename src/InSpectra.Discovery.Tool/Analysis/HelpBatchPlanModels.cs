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
            ExpectedCommands: ReadStringList(item, "expectedCommands"),
            ExpectedOptions: ReadStringList(item, "expectedOptions"),
            ExpectedArguments: ReadStringList(item, "expectedArguments"),
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

    private static IReadOnlyList<string> ReadStringList(JsonObject item, string propertyName)
        => item[propertyName] is not JsonArray values
            ? []
            : values.OfType<JsonValue>()
                .Select(value => value.GetValue<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
}

internal sealed record HelpBatchItem(
    string PackageId,
    string Version,
    string? CommandName,
    string? CliFramework,
    IReadOnlyList<string> ExpectedCommands,
    IReadOnlyList<string> ExpectedOptions,
    IReadOnlyList<string> ExpectedArguments,
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
