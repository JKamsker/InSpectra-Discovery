namespace InSpectra.Discovery.Tool.Docs;

using InSpectra.Discovery.Tool.Docs.Commands;

using InSpectra.Discovery.Tool.Docs.Services;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

internal static class DocsModule
{
    public static IServiceCollection AddDocsModule(this IServiceCollection services)
    {
        services.AddTransient<DocsCommandService>();
        services.AddTransient<DocsPartialPlanCommandService>();
        services.AddTransient<DocsPartialReanalysisCommandService>();
        services.AddTransient<DocsRebuildIndexesCommand>();
        services.AddTransient<DocsReconcileStoredOpenCliCommand>();
        services.AddTransient<DocsReconcileLegacyPartialMetadataCommand>();
        services.AddTransient<DocsExportLatestPartialsPlanCommand>();
        services.AddTransient<DocsReanalyzeLatestPartialsCommand>();
        services.AddTransient<DocsRegenerateNativeOpenCliCommand>();
        services.AddTransient<DocsRegenerateStartupHookOpenCliCommand>();
        services.AddTransient<HelpCrawlArtifactRegenerationService>();
        services.AddTransient<DocsRegenerateHelpCrawlsCommand>();
        services.AddTransient<DocsRegenerateXmldocOpenCliCommand>();
        services.AddTransient<DocsBrowserIndexCommand>();
        services.AddTransient<DocsGitHubPagesSnapshotCommand>();
        services.AddTransient<DocsFullyIndexedReportCommand>();

        return services;
    }

    public static void RegisterCommands(IConfigurator config)
    {
        config.AddBranch("docs", docs =>
        {
            docs.SetDescription("Generate derived discovery documentation artifacts.");
            docs.AddCommand<DocsRebuildIndexesCommand>("rebuild-indexes").WithDescription("Rebuild package summaries, index/all.json, and index/index.json from indexed metadata.");
            docs.AddCommand<DocsReconcileStoredOpenCliCommand>("reconcile-stored-opencli").WithDescription("Revalidate stored opencli.json files and sync indexed metadata from their actual artifact provenance.");
            docs.AddCommand<DocsReconcileLegacyPartialMetadataCommand>("reconcile-legacy-partials").WithDescription("Backfill explicit introspection failure metadata for legacy partial results that have no stored analysis artifacts.");
            docs.AddCommand<DocsExportLatestPartialsPlanCommand>("export-latest-partials-plan").WithDescription("Export a promotion-ready expected.json plan for latest partial package records.");
            docs.AddCommand<DocsReanalyzeLatestPartialsCommand>("reanalyze-latest-partials").WithDescription("Rerun the latest partial package records through current auto analysis and reapply the results into index/state.");
            docs.AddCommand<DocsRegenerateNativeOpenCliCommand>("regenerate-native-opencli").WithDescription("Resanitize native OpenCLI artifacts from stored opencli.json files.");
            docs.AddCommand<DocsRegenerateStartupHookOpenCliCommand>("regenerate-startup-hook-opencli").WithDescription("Resanitize startup-hook OpenCLI artifacts from stored opencli.json files.");
            docs.AddCommand<DocsRegenerateHelpCrawlsCommand>("regenerate-help-crawls").WithDescription("Regenerate generic help OpenCLI artifacts from stored crawl.json captures.");
            docs.AddCommand<DocsRegenerateXmldocOpenCliCommand>("regenerate-xmldoc-opencli").WithDescription("Regenerate XMLDoc-synthesized OpenCLI artifacts from stored xmldoc.xml files.");
            docs.AddCommand<DocsBrowserIndexCommand>("browser-index").WithDescription("Build the lightweight browser index from index/all.json.");
            docs.AddCommand<DocsGitHubPagesSnapshotCommand>("github-pages-snapshot").WithDescription("Build a minified GitHub Pages snapshot from selected index JSON artifacts.");
            docs.AddCommand<DocsFullyIndexedReportCommand>("fully-indexed-report").WithDescription("Build the fully indexed package documentation coverage report.");
        });
    }
}
