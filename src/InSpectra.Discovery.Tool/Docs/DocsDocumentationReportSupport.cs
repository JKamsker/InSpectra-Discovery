using System.Text.Json.Nodes;

internal sealed record DocumentationReportBuildResult(
    int PackageCount,
    int FullyDocumentedCount,
    int IncompleteCount,
    IReadOnlyList<string> Lines);

internal static class DocsDocumentationReportSupport
{
    public static DocumentationReportBuildResult BuildReport(
        string repositoryRoot,
        JsonObject manifest,
        CancellationToken cancellationToken)
    {
        var reportRows = new List<ReportRow>();

        foreach (var packageNode in manifest["packages"]?.AsArray() ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (packageNode is not JsonObject package)
            {
                continue;
            }

            if (TryCreateReportRow(repositoryRoot, package, out var row))
            {
                reportRows.Add(row);
            }
        }

        var sortedRows = reportRows
            .OrderBy(row => row.OverallComplete ? 1 : 0)
            .ThenBy(row => row.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var fullyDocumentedCount = sortedRows.Count(row => row.OverallComplete);
        var incompleteCount = sortedRows.Count - fullyDocumentedCount;

        return new DocumentationReportBuildResult(
            PackageCount: sortedRows.Count,
            FullyDocumentedCount: fullyDocumentedCount,
            IncompleteCount: incompleteCount,
            Lines: DocsDocumentationReportFormattingSupport.BuildDocumentationReport(sortedRows, fullyDocumentedCount, incompleteCount));
    }

    private static bool TryCreateReportRow(string repositoryRoot, JsonObject package, out ReportRow row)
    {
        row = default!;

        var latestPaths = package["latestPaths"]?.AsObject();
        var metadataRelativePath = latestPaths?["metadataPath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(metadataRelativePath))
        {
            return false;
        }

        var metadataPath = Path.Combine(repositoryRoot, metadataRelativePath);
        if (!PromotionArtifactSupport.TryLoadJsonObject(metadataPath, out var metadata) || metadata is null)
        {
            return false;
        }

        var packageStatus = metadata["status"]?.GetValue<string>();
        var openCliClassification = metadata["introspection"]?["opencli"]?["classification"]?.GetValue<string>();
        if (!string.Equals(packageStatus, "ok", StringComparison.OrdinalIgnoreCase)
            || !IsReportableOpenCliClassification(openCliClassification))
        {
            return false;
        }

        if (!OpenCliArtifactLoadSupport.TryLoadFirstValidOpenCliDocument(
            repositoryRoot,
            [
                latestPaths?["opencliPath"]?.GetValue<string>(),
                metadata["artifacts"]?["opencliPath"]?.GetValue<string>(),
                metadata["steps"]?["opencli"]?["path"]?.GetValue<string>(),
                package["versions"]?.AsArray().OfType<JsonObject>().FirstOrDefault()?["paths"]?["opencliPath"]?.GetValue<string>(),
            ],
            out var openCli,
            out _))
        {
            return false;
        }

        var artifactSource = FirstNonEmpty(
            openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>(),
            metadata["artifacts"]?["opencliSource"]?.GetValue<string>(),
            metadata["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>(),
            metadata["introspection"]?["opencli"]?["artifactSource"]?.GetValue<string>());
        if (!string.Equals(artifactSource, "tool-output", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stats = new DocumentationStats();
        AddOptionStats(stats, "[root]", openCli?["options"] as JsonArray);
        AddArgumentStats(stats, "[root]", openCli?["arguments"] as JsonArray);
        AddCommandStats(stats, string.Empty, openCli?["commands"] as JsonArray);

        var packageId = metadata["packageId"]?.GetValue<string>() ?? package["packageId"]?.GetValue<string>() ?? string.Empty;
        var version = metadata["version"]?.GetValue<string>() ?? string.Empty;
        var xmlDoc = metadata["introspection"]?["xmldoc"]?["classification"]?.GetValue<string>() ?? "n/a";

        row = new ReportRow(
            PackageId: packageId,
            Version: version,
            PackageStatus: packageStatus ?? string.Empty,
            OpenCliClassification: openCliClassification ?? string.Empty,
            XmlDocClassification: xmlDoc,
            CommandsCoverage: $"{stats.DescribedCommands}/{stats.VisibleCommands}",
            OptionsCoverage: $"{stats.DescribedOptions}/{stats.VisibleOptions}",
            ArgumentsCoverage: $"{stats.DescribedArguments}/{stats.VisibleArguments}",
            ExamplesCoverage: $"{stats.LeafCommandsWithExamples}/{stats.VisibleLeafCommands}",
            OverallComplete: stats.IsComplete,
            Anchor: $"pkg-{DocsDocumentationReportFormattingSupport.ToAnchorSlug(packageId)}",
            MissingCommandDescriptions: DocsDocumentationReportFormattingSupport.FormatListOrNone(stats.MissingCommandDescriptions),
            MissingOptionDescriptions: DocsDocumentationReportFormattingSupport.FormatListOrNone(stats.MissingOptionDescriptions),
            MissingArgumentDescriptions: DocsDocumentationReportFormattingSupport.FormatListOrNone(stats.MissingArgumentDescriptions),
            MissingLeafExamples: DocsDocumentationReportFormattingSupport.FormatListOrNone(stats.MissingLeafExamples));
        return true;
    }

    private static void AddCommandStats(DocumentationStats stats, string parentPath, JsonArray? commands)
    {
        foreach (var commandNode in GetVisibleItems(commands))
        {
            var command = commandNode.AsObject();
            var commandName = command["name"]?.GetValue<string>() ?? string.Empty;
            var commandPath = string.IsNullOrWhiteSpace(parentPath) ? commandName : $"{parentPath} {commandName}";
            stats.VisibleCommands++;
            if (HasText(command["description"]))
            {
                stats.DescribedCommands++;
            }
            else
            {
                stats.MissingCommandDescriptions.Add(commandPath);
            }

            AddOptionStats(stats, commandPath, command["options"] as JsonArray);
            AddArgumentStats(stats, commandPath, command["arguments"] as JsonArray);

            var children = GetVisibleItems(command["commands"] as JsonArray).ToList();
            if (children.Count == 0)
            {
                stats.VisibleLeafCommands++;
                var examples = (command["examples"] as JsonArray)?.Where(HasText).ToList() ?? [];
                if (examples.Count > 0)
                {
                    stats.LeafCommandsWithExamples++;
                }
                else
                {
                    stats.MissingLeafExamples.Add(commandPath);
                }
            }

            AddCommandStats(stats, commandPath, command["commands"] as JsonArray);
        }
    }

    private static void AddOptionStats(DocumentationStats stats, string location, JsonArray? options)
    {
        foreach (var optionNode in GetVisibleItems(options))
        {
            var option = optionNode.AsObject();
            stats.VisibleOptions++;
            var optionName = option["name"]?.GetValue<string>() ?? string.Empty;
            var qualifiedName = string.IsNullOrWhiteSpace(location) ? optionName : $"{location} {optionName}";
            if (HasText(option["description"]))
            {
                stats.DescribedOptions++;
            }
            else
            {
                stats.MissingOptionDescriptions.Add(qualifiedName);
            }
        }
    }

    private static void AddArgumentStats(DocumentationStats stats, string location, JsonArray? arguments)
    {
        foreach (var argumentNode in GetVisibleItems(arguments))
        {
            var argument = argumentNode.AsObject();
            stats.VisibleArguments++;
            var argumentName = argument["name"]?.GetValue<string>() ?? string.Empty;
            var qualifiedName = string.IsNullOrWhiteSpace(location) ? $"<{argumentName}>" : $"{location} <{argumentName}>";
            if (HasText(argument["description"]))
            {
                stats.DescribedArguments++;
            }
            else
            {
                stats.MissingArgumentDescriptions.Add(qualifiedName);
            }
        }
    }

    private static IEnumerable<JsonObject> GetVisibleItems(JsonArray? items)
        => items?.OfType<JsonObject>().Where(item => item["hidden"]?.GetValue<bool?>() != true) ?? [];

    private static bool HasText(JsonNode? value)
        => value is JsonValue jsonValue &&
           jsonValue.TryGetValue<string>(out var text) &&
           !string.IsNullOrWhiteSpace(text);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsReportableOpenCliClassification(string? classification)
        => string.Equals(classification, "json-ready", StringComparison.OrdinalIgnoreCase)
           || string.Equals(classification, "json-ready-with-nonzero-exit", StringComparison.OrdinalIgnoreCase);

    private sealed class DocumentationStats
    {
        public int VisibleCommands { get; set; }
        public int DescribedCommands { get; set; }
        public int VisibleOptions { get; set; }
        public int DescribedOptions { get; set; }
        public int VisibleArguments { get; set; }
        public int DescribedArguments { get; set; }
        public int VisibleLeafCommands { get; set; }
        public int LeafCommandsWithExamples { get; set; }
        public List<string> MissingCommandDescriptions { get; } = [];
        public List<string> MissingOptionDescriptions { get; } = [];
        public List<string> MissingArgumentDescriptions { get; } = [];
        public List<string> MissingLeafExamples { get; } = [];
        public bool IsComplete =>
            VisibleCommands == DescribedCommands &&
            VisibleOptions == DescribedOptions &&
            VisibleArguments == DescribedArguments &&
            VisibleLeafCommands == LeafCommandsWithExamples;
    }
}
