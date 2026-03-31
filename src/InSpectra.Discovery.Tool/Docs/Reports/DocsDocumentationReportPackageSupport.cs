namespace InSpectra.Discovery.Tool.Docs.Reports;

using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using InSpectra.Discovery.Tool.Promotion.Artifacts;


using System.Text.Json.Nodes;

internal static class DocsDocumentationReportPackageSupport
{
    public static bool TryCreateReportRow(string repositoryRoot, JsonObject package, out ReportRow row)
    {
        row = default!;

        if (!TryLoadReportablePackage(repositoryRoot, package, out var context))
        {
            return false;
        }

        var stats = DocsDocumentationCoverageSupport.CollectStats(context.OpenCli);
        row = new ReportRow(
            PackageId: context.PackageId,
            Version: context.Version,
            PackageStatus: context.PackageStatus,
            OpenCliClassification: context.OpenCliClassification,
            XmlDocClassification: context.XmlDocClassification,
            CommandsCoverage: $"{stats.DescribedCommands}/{stats.VisibleCommands}",
            OptionsCoverage: $"{stats.DescribedOptions}/{stats.VisibleOptions}",
            ArgumentsCoverage: $"{stats.DescribedArguments}/{stats.VisibleArguments}",
            ExamplesCoverage: $"{stats.LeafCommandsWithExamples}/{stats.VisibleLeafCommands}",
            OverallComplete: stats.IsComplete,
            Anchor: $"pkg-{DocsDocumentationReportFormattingSupport.ToAnchorSlug(context.PackageId)}",
            MissingCommandDescriptions: DocsDocumentationReportFormattingSupport.FormatListOrNone(stats.MissingCommandDescriptions),
            MissingOptionDescriptions: DocsDocumentationReportFormattingSupport.FormatListOrNone(stats.MissingOptionDescriptions),
            MissingArgumentDescriptions: DocsDocumentationReportFormattingSupport.FormatListOrNone(stats.MissingArgumentDescriptions),
            MissingLeafExamples: DocsDocumentationReportFormattingSupport.FormatListOrNone(stats.MissingLeafExamples));
        return true;
    }

    private static bool TryLoadReportablePackage(
        string repositoryRoot,
        JsonObject package,
        out DocumentationReportPackageContext context)
    {
        context = default!;

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

        if (!TryLoadOpenCli(repositoryRoot, package, latestPaths, metadata, out var openCli))
        {
            return false;
        }

        if (openCli is null)
        {
            return false;
        }

        var artifactSource = FirstNonEmpty(
            openCli["x-inspectra"]?["artifactSource"]?.GetValue<string>(),
            metadata["artifacts"]?["opencliSource"]?.GetValue<string>(),
            metadata["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>(),
            metadata["introspection"]?["opencli"]?["artifactSource"]?.GetValue<string>());
        if (!string.Equals(artifactSource, "tool-output", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        context = new DocumentationReportPackageContext(
            PackageId: metadata["packageId"]?.GetValue<string>() ?? package["packageId"]?.GetValue<string>() ?? string.Empty,
            Version: metadata["version"]?.GetValue<string>() ?? string.Empty,
            PackageStatus: packageStatus ?? string.Empty,
            OpenCliClassification: openCliClassification ?? string.Empty,
            XmlDocClassification: metadata["introspection"]?["xmldoc"]?["classification"]?.GetValue<string>() ?? "n/a",
            OpenCli: openCli);
        return true;
    }

    private static bool TryLoadOpenCli(
        string repositoryRoot,
        JsonObject package,
        JsonObject? latestPaths,
        JsonObject metadata,
        out JsonObject openCli)
    {
        openCli = null!;

        if (!OpenCliArtifactLoadSupport.TryLoadFirstValidOpenCliDocument(
            repositoryRoot,
            [
                latestPaths?["opencliPath"]?.GetValue<string>(),
                metadata["artifacts"]?["opencliPath"]?.GetValue<string>(),
                metadata["steps"]?["opencli"]?["path"]?.GetValue<string>(),
                package["versions"]?.AsArray().OfType<JsonObject>().FirstOrDefault()?["paths"]?["opencliPath"]?.GetValue<string>(),
            ],
            out var loadedOpenCli,
            out _)
            || loadedOpenCli is null)
        {
            return false;
        }

        openCli = loadedOpenCli;
        return true;
    }

    private static bool IsReportableOpenCliClassification(string? classification)
        => string.Equals(classification, "json-ready", StringComparison.OrdinalIgnoreCase)
           || string.Equals(classification, "json-ready-with-nonzero-exit", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

internal sealed record DocumentationReportPackageContext(
    string PackageId,
    string Version,
    string PackageStatus,
    string OpenCliClassification,
    string XmlDocClassification,
    JsonObject OpenCli);

