namespace InSpectra.Discovery.Tool.OpenCli;

using System.Text.Json.Nodes;

internal static class OpenCliMetrics
{
    public static OpenCliMetricsResult GetFromPath(string? openCliPath)
    {
        if (string.IsNullOrWhiteSpace(openCliPath) || !File.Exists(openCliPath))
        {
            return OpenCliMetricsResult.Empty;
        }

        return OpenCliDocumentValidator.TryLoadValidDocument(openCliPath, out var document, out _)
            ? GetFromDocument(document)
            : OpenCliMetricsResult.Empty;
    }

    public static OpenCliMetricsResult GetFromDocument(JsonNode? openCliDocument)
    {
        if (openCliDocument is not JsonObject root)
        {
            return OpenCliMetricsResult.Empty;
        }

        var state = new MetricsState();
        AddOptionMetrics(state, root["options"] as JsonArray);
        AddArgumentMetrics(state, root["arguments"] as JsonArray);
        AddCommandMetrics(state, root["commands"] as JsonArray);

        var documentedItemCount =
            state.DescribedCommandCount +
            state.DescribedOptionCount +
            state.DescribedArgumentCount +
            state.LeafCommandWithExampleCount;
        var documentableItemCount =
            state.CommandCount +
            state.VisibleOptionCount +
            state.VisibleArgumentCount +
            state.VisibleLeafCommandCount;
        var documentationCoveragePercent = documentableItemCount > 0
            ? Math.Round((documentedItemCount * 100.0) / documentableItemCount, 4)
            : 0.0;

        return new OpenCliMetricsResult(
            state.CommandGroupCount,
            state.VisibleLeafCommandCount,
            documentationCoveragePercent,
            documentedItemCount,
            documentableItemCount);
    }

    public static JsonObject ApplyToPackageSummary(JsonObject summary, OpenCliMetricsResult metrics)
    {
        var clone = summary.DeepClone().AsObject();
        clone["commandGroupCount"] = metrics.CommandGroupCount;
        clone["commandCount"] = metrics.CommandCount;
        return clone;
    }

    public static IReadOnlyList<JsonObject> SortPackageSummariesForAllIndex(IEnumerable<JsonObject> packageSummaries, string repositoryRoot)
        => packageSummaries
            .Select(summary =>
            {
                var metrics = ResolveOpenCliMetrics(summary, repositoryRoot);
                return new
                {
                    Summary = ApplyToPackageSummary(summary, metrics),
                    Metrics = metrics,
                };
            })
            .OrderByDescending(item => item.Metrics.CommandGroupCount)
            .ThenByDescending(item => item.Metrics.CommandCount)
            .ThenByDescending(item => item.Metrics.DocumentationCoveragePercent)
            .ThenBy(item => item.Summary["packageId"]?.GetValue<string>() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Summary)
            .ToList();

    private static OpenCliMetricsResult ResolveOpenCliMetrics(JsonObject summary, string repositoryRoot)
    {
        var latestPaths = summary["latestPaths"] as JsonObject;
        var versionedOpenCliPath = summary["versions"]?.AsArray().OfType<JsonObject>().FirstOrDefault()?["paths"]?["opencliPath"]?.GetValue<string>();

        if (OpenCliArtifactLoadSupport.TryLoadFirstValidOpenCliDocument(
            repositoryRoot,
            [latestPaths?["opencliPath"]?.GetValue<string>(), versionedOpenCliPath],
            out var directDocument,
            out _))
        {
            return GetFromDocument(directDocument);
        }

        var metadataPath = OpenCliArtifactLoadSupport.ResolveExistingPath(
            repositoryRoot,
            latestPaths?["metadataPath"]?.GetValue<string>());
        if (metadataPath is not null)
        {
            if (PromotionArtifactSupport.TryLoadJsonObject(metadataPath, out var metadata) && metadata is not null
                && OpenCliArtifactLoadSupport.TryLoadFirstValidOpenCliDocument(
                    repositoryRoot,
                    [
                        metadata["artifacts"]?["opencliPath"]?.GetValue<string>(),
                        metadata["steps"]?["opencli"]?["path"]?.GetValue<string>(),
                        versionedOpenCliPath,
                    ],
                    out var metadataDocument,
                    out _))
            {
                return GetFromDocument(metadataDocument);
            }
        }

        return OpenCliMetricsResult.Empty;
    }

    private static void AddOptionMetrics(MetricsState state, JsonArray? options)
    {
        foreach (var option in GetVisibleItems(options))
        {
            state.VisibleOptionCount++;
            if (HasText(option["description"]))
            {
                state.DescribedOptionCount++;
            }
        }
    }

    private static void AddArgumentMetrics(MetricsState state, JsonArray? arguments)
    {
        foreach (var argument in GetVisibleItems(arguments))
        {
            state.VisibleArgumentCount++;
            if (HasText(argument["description"]))
            {
                state.DescribedArgumentCount++;
            }
        }
    }

    private static void AddCommandMetrics(MetricsState state, JsonArray? commands)
    {
        foreach (var command in GetVisibleItems(commands))
        {
            state.CommandCount++;
            if (HasText(command["description"]))
            {
                state.DescribedCommandCount++;
            }

            AddOptionMetrics(state, command["options"] as JsonArray);
            AddArgumentMetrics(state, command["arguments"] as JsonArray);

            var childCommands = GetVisibleItems(command["commands"] as JsonArray).ToList();
            if (childCommands.Count > 0)
            {
                state.CommandGroupCount++;
            }
            else
            {
                state.VisibleLeafCommandCount++;
                var examples = (command["examples"] as JsonArray)?
                    .Select(node => node?.GetValue<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList() ?? [];

                if (examples.Count > 0)
                {
                    state.LeafCommandWithExampleCount++;
                }
            }

            AddCommandMetrics(state, command["commands"] as JsonArray);
        }
    }

    private static IEnumerable<JsonObject> GetVisibleItems(JsonArray? items)
        => items?
            .OfType<JsonObject>()
            .Where(item => item["hidden"]?.GetValue<bool?>() != true) ?? [];

    private static bool HasText(JsonNode? value)
        => value is JsonValue jsonValue &&
           jsonValue.TryGetValue<string>(out var text) &&
           !string.IsNullOrWhiteSpace(text);

    private sealed class MetricsState
    {
        public int CommandGroupCount { get; set; }
        public int CommandCount { get; set; }
        public int DescribedCommandCount { get; set; }
        public int VisibleOptionCount { get; set; }
        public int DescribedOptionCount { get; set; }
        public int VisibleArgumentCount { get; set; }
        public int DescribedArgumentCount { get; set; }
        public int VisibleLeafCommandCount { get; set; }
        public int LeafCommandWithExampleCount { get; set; }
    }
}

internal sealed record OpenCliMetricsResult(
    int CommandGroupCount,
    int CommandCount,
    double DocumentationCoveragePercent,
    int DocumentedItemCount,
    int DocumentableItemCount)
{
    public static OpenCliMetricsResult Empty { get; } = new(0, 0, 0.0, 0, 0);
}

