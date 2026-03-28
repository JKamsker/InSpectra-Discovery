using System.Text.Json.Nodes;

internal sealed class DocsCommandService
{
    public async Task<int> RebuildIndexesAsync(
        string repositoryRoot,
        bool writeBrowserIndex,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var result = RepositoryPackageIndexBuilder.Rebuild(root, writeBrowserIndex);
        var output = ToolRuntime.CreateOutput();

        return await output.WriteSuccessAsync(
            new
            {
                packageCount = result.PackageCount,
                allIndexPath = result.AllIndexPath,
                browserIndexPath = result.BrowserIndexPath,
            },
            [
                new SummaryRow("Packages", result.PackageCount.ToString()),
                new SummaryRow("All index", result.AllIndexPath),
                new SummaryRow("Browser index", result.BrowserIndexPath ?? "skipped"),
            ],
            json,
            cancellationToken);
    }

    public async Task<int> BuildBrowserIndexAsync(
        string repositoryRoot,
        string allIndexPath,
        string outputPath,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var allIndexFile = Path.GetFullPath(Path.Combine(root, allIndexPath));
        var outputFile = Path.GetFullPath(Path.Combine(root, outputPath));
        var allIndex = JsonNode.Parse(await File.ReadAllTextAsync(allIndexFile, cancellationToken))?.AsObject()
            ?? throw new InvalidOperationException($"Manifest '{allIndexFile}' is empty.");
        var packageNodes = allIndex["packages"]?.AsArray() ?? [];
        var packages = new List<object>();
        var now = DateTimeOffset.UtcNow;

        foreach (var packageNode in packageNodes)
        {
            if (packageNode is not JsonObject package)
            {
                continue;
            }

            var latestVersionRecord = package["versions"]?.AsArray().FirstOrDefault() as JsonObject;
            var packageId = package["packageId"]?.GetValue<string>() ?? string.Empty;
            var latestVersion = package["latestVersion"]?.GetValue<string>() ?? string.Empty;
            var packageTimestamps = RepositoryPackageIndexBuilder.ResolvePackageTimestamps(package);
            packages.Add(new
            {
                packageId,
                commandName = latestVersionRecord?["command"]?.GetValue<string>(),
                cliFramework = package["cliFramework"]?.GetValue<string>(),
                versionCount = package["versions"]?.AsArray().Count ?? 0,
                latestVersion,
                createdAt = packageTimestamps.CreatedAt,
                updatedAt = packageTimestamps.UpdatedAt,
                completeness = GetCompletenessLabel(package["latestStatus"]?.GetValue<string>()),
                packageIconUrl = string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(latestVersion)
                    ? null
                    : $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/{latestVersion.ToLowerInvariant()}/icon",
                totalDownloads = package["totalDownloads"]?.GetValue<long?>(),
                commandCount = package["commandCount"]?.GetValue<int?>() ?? 0,
                commandGroupCount = package["commandGroupCount"]?.GetValue<int?>() ?? 0,
            });
        }

        var createdAt = ResolveDocumentCreatedAt(
            outputFile,
            allIndex["createdAt"]?.GetValue<string>() ?? allIndex["generatedAt"]?.GetValue<string>(),
            now);

        var browserIndex = new
        {
            schemaVersion = 1,
            createdAt,
            updatedAt = now,
            generatedAt = now,
            packageCount = packages.Count,
            packages,
        };

        RepositoryPathResolver.WriteJsonFile(outputFile, browserIndex);
        var output = ToolRuntime.CreateOutput();
        return await output.WriteSuccessAsync(
            browserIndex,
            [
                new SummaryRow("Packages", packages.Count.ToString()),
                new SummaryRow("Output", outputFile),
            ],
            json,
            cancellationToken);
    }

    public async Task<int> BuildFullyIndexedDocumentationReportAsync(
        string repositoryRoot,
        string manifestPath,
        string outputPath,
        bool json,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var manifestFile = Path.GetFullPath(Path.Combine(root, manifestPath));
        var reportFile = Path.GetFullPath(Path.Combine(root, outputPath));
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestFile, cancellationToken))?.AsObject()
            ?? throw new InvalidOperationException($"Manifest '{manifestFile}' is empty.");
        var reportRows = new List<ReportRow>();

        foreach (var packageNode in manifest["packages"]?.AsArray() ?? [])
        {
            if (packageNode is not JsonObject package)
            {
                continue;
            }

            var latestPaths = package["latestPaths"]?.AsObject();
            var metadataRelativePath = latestPaths?["metadataPath"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(metadataRelativePath))
            {
                continue;
            }

            var metadataPath = Path.Combine(root, metadataRelativePath);
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            var metadata = JsonNode.Parse(await File.ReadAllTextAsync(metadataPath, cancellationToken))?.AsObject();
            if (metadata is null)
            {
                continue;
            }

            var packageStatus = metadata["status"]?.GetValue<string>();
            var openCliClassification = metadata["introspection"]?["opencli"]?["classification"]?.GetValue<string>();
            var artifactSource = metadata["artifacts"]?["opencliSource"]?.GetValue<string>()
                ?? metadata["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>();

            if (!string.Equals(packageStatus, "ok", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(openCliClassification, "json-ready", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(artifactSource, "synthesized-from-xmldoc", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var openCliRelativePath = metadata["artifacts"]?["opencliPath"]?.GetValue<string>()
                ?? latestPaths?["opencliPath"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(openCliRelativePath))
            {
                continue;
            }

            var openCliPath = Path.Combine(root, openCliRelativePath);
            if (!File.Exists(openCliPath))
            {
                continue;
            }

            var openCli = JsonNode.Parse(await File.ReadAllTextAsync(openCliPath, cancellationToken));
            var stats = new DocumentationStats();
            AddOptionStats(stats, "[root]", openCli?["options"] as JsonArray);
            AddArgumentStats(stats, "[root]", openCli?["arguments"] as JsonArray);
            AddCommandStats(stats, string.Empty, openCli?["commands"] as JsonArray);

            var packageId = metadata["packageId"]?.GetValue<string>() ?? package["packageId"]?.GetValue<string>() ?? string.Empty;
            var version = metadata["version"]?.GetValue<string>() ?? string.Empty;
            var xmlDoc = metadata["introspection"]?["xmldoc"]?["classification"]?.GetValue<string>() ?? "n/a";

            reportRows.Add(new ReportRow(
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
                Anchor: $"pkg-{ToAnchorSlug(packageId)}",
                MissingCommandDescriptions: FormatListOrNone(stats.MissingCommandDescriptions),
                MissingOptionDescriptions: FormatListOrNone(stats.MissingOptionDescriptions),
                MissingArgumentDescriptions: FormatListOrNone(stats.MissingArgumentDescriptions),
                MissingLeafExamples: FormatListOrNone(stats.MissingLeafExamples)));
        }

        var sortedRows = reportRows
            .OrderBy(row => row.OverallComplete ? 1 : 0)
            .ThenBy(row => row.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var fullyDocumentedCount = sortedRows.Count(row => row.OverallComplete);
        var incompleteCount = sortedRows.Count - fullyDocumentedCount;
        var lines = BuildDocumentationReport(sortedRows, fullyDocumentedCount, incompleteCount);
        RepositoryPathResolver.WriteLines(reportFile, lines);

        var result = new
        {
            packageCount = sortedRows.Count,
            fullyDocumentedCount,
            incompleteCount,
            outputPath = reportFile,
        };

        var output = ToolRuntime.CreateOutput();
        return await output.WriteSuccessAsync(
            result,
            [
                new SummaryRow("Packages in scope", sortedRows.Count.ToString()),
                new SummaryRow("Fully documented", fullyDocumentedCount.ToString()),
                new SummaryRow("Incomplete", incompleteCount.ToString()),
                new SummaryRow("Output", reportFile),
            ],
            json,
            cancellationToken);
    }

    private static string GetCompletenessLabel(string? latestStatus)
        => latestStatus switch
        {
            "ok" => "full",
            "partial" => "partial",
            _ => latestStatus ?? string.Empty,
        };

    private static DateTimeOffset ResolveDocumentCreatedAt(string outputFile, string? fallback, DateTimeOffset now)
    {
        if (File.Exists(outputFile))
        {
            var existing = JsonNode.Parse(File.ReadAllText(outputFile))?.AsObject();
            if (DateTimeOffset.TryParse(existing?["createdAt"]?.GetValue<string>(), out var parsedCreatedAt))
            {
                return parsedCreatedAt.ToUniversalTime();
            }

            if (DateTimeOffset.TryParse(existing?["generatedAt"]?.GetValue<string>(), out var parsedGeneratedAt))
            {
                return parsedGeneratedAt.ToUniversalTime();
            }
        }

        if (DateTimeOffset.TryParse(fallback, out var parsedFallback))
        {
            return parsedFallback.ToUniversalTime();
        }

        return now;
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

    private static IEnumerable<JsonNode> GetVisibleItems(JsonArray? items)
        => items?.OfType<JsonNode>().Where(item => item["hidden"]?.GetValue<bool?>() != true) ?? [];

    private static bool HasText(JsonNode? value)
        => value is JsonValue jsonValue &&
           jsonValue.TryGetValue<string>(out var text) &&
           !string.IsNullOrWhiteSpace(text);

    private static string ToAnchorSlug(string value)
    {
        var normalized = new string(value.ToLowerInvariant().Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(normalized) ? "package" : normalized;
    }

    private static string FormatListOrNone(IReadOnlyCollection<string> items)
        => items.Count == 0 ? "None" : string.Join(", ", items.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).Distinct(StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<string> BuildDocumentationReport(
        IReadOnlyList<ReportRow> rows,
        int fullyDocumentedCount,
        int incompleteCount)
    {
        var lines = new List<string>
        {
            "# Fully Indexed Package Documentation Report",
            string.Empty,
            $"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ssK}",
            string.Empty,
            "Scope: latest package entries with status ok, whose native OpenCLI artifact is json-ready, and whose OpenCLI was not synthesized-from-xmldoc.",
            string.Empty,
            "Completeness rule: visible commands, options, and arguments must all have non-empty descriptions, and every visible leaf command must have at least one non-empty example.",
            string.Empty,
            "Hidden commands, options, and arguments are excluded from the score.",
            string.Empty,
            $"Packages in scope: {rows.Count}",
            string.Empty,
            $"Fully documented: {fullyDocumentedCount}",
            string.Empty,
            $"Incomplete: {incompleteCount}",
            string.Empty,
            "| Package | Version | Status | XML | Cmd Docs | Opt Docs | Arg Docs | Leaf Examples | Overall |",
            "| --- | --- | --- | --- | --- | --- | --- | --- | --- |",
        };

        foreach (var row in rows)
        {
            lines.Add($"| [{row.PackageId}](#{row.Anchor}) | {row.Version} | {row.PackageStatus} | {row.XmlDocClassification} | {row.CommandsCoverage} | {row.OptionsCoverage} | {row.ArgumentsCoverage} | {row.ExamplesCoverage} | {(row.OverallComplete ? "PASS" : "FAIL")} |");
        }

        lines.Add(string.Empty);
        lines.Add("## Package Details");
        foreach (var row in rows)
        {
            lines.Add(string.Empty);
            lines.Add($"<a id=\"{row.Anchor}\"></a>");
            lines.Add($"### {row.PackageId}");
            lines.Add(string.Empty);
            lines.Add($"- Version: `{row.Version}`");
            lines.Add($"- Package status: `{row.PackageStatus}`");
            lines.Add($"- OpenCLI classification: `{row.OpenCliClassification}`");
            lines.Add($"- XMLDoc classification: `{row.XmlDocClassification}`");
            lines.Add($"- Command documentation: `{row.CommandsCoverage}`");
            lines.Add($"- Option documentation: `{row.OptionsCoverage}`");
            lines.Add($"- Argument documentation: `{row.ArgumentsCoverage}`");
            lines.Add($"- Leaf command examples: `{row.ExamplesCoverage}`");
            lines.Add($"- Overall: `{(row.OverallComplete ? "PASS" : "FAIL")}`");
            lines.Add($"- Missing command descriptions: {row.MissingCommandDescriptions}");
            lines.Add($"- Missing option descriptions: {row.MissingOptionDescriptions}");
            lines.Add($"- Missing argument descriptions: {row.MissingArgumentDescriptions}");
            lines.Add($"- Missing leaf command examples: {row.MissingLeafExamples}");
        }

        return lines;
    }

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

    private sealed record ReportRow(
        string PackageId,
        string Version,
        string PackageStatus,
        string OpenCliClassification,
        string XmlDocClassification,
        string CommandsCoverage,
        string OptionsCoverage,
        string ArgumentsCoverage,
        string ExamplesCoverage,
        bool OverallComplete,
        string Anchor,
        string MissingCommandDescriptions,
        string MissingOptionDescriptions,
        string MissingArgumentDescriptions,
        string MissingLeafExamples);
}
