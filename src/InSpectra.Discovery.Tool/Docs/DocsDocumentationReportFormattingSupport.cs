namespace InSpectra.Discovery.Tool.Docs;

internal static class DocsDocumentationReportFormattingSupport
{
    public static string ToAnchorSlug(string value)
    {
        var normalized = new string(value.ToLowerInvariant().Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(normalized) ? "package" : normalized;
    }

    public static string FormatListOrNone(IReadOnlyCollection<string> items)
        => items.Count == 0 ? "None" : string.Join(", ", items.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).Distinct(StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<string> BuildDocumentationReport(
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
            "Scope: latest package entries with status ok, whose OpenCLI classification is json-ready or json-ready-with-nonzero-exit, and whose resolved OpenCLI provenance is tool-output.",
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
}

internal sealed record ReportRow(
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

