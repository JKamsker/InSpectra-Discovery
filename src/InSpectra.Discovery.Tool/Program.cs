using Spectre.Console.Cli;
using System.Reflection;

ToolRuntime.Initialize();
var output = ToolRuntime.CreateOutput();
var jsonRequested = args.Any(arg => string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase));

try
{
    var app = new CommandApp();
    app.Configure(config =>
    {
        config.PropagateExceptions();
        config.SetApplicationName("inspectra-discovery");
        config.SetApplicationVersion(Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0");

        config.AddBranch("catalog", catalog =>
        {
            catalog.SetDescription("NuGet discovery and filtering.");
            catalog.AddCommand<CatalogBuildCommand>("build").WithDescription("Build the current ranked dotnet-tool index from NuGet.");

            catalog.AddBranch("delta", delta =>
            {
                delta.SetDescription("Incremental catalog discovery and Spectre CLI queue narrowing.");
                delta.AddCommand<CatalogDeltaDiscoverCommand>("discover").WithDescription("Discover added or updated dotnet tools since the saved catalog cursor.");
                delta.AddCommand<CatalogDeltaQueueSpectreCliCommand>("queue-spectre-cli").WithDescription("Narrow the latest delta to Spectre.Console.Cli evidence and emit a queue.");
            });

            catalog.AddBranch("filter", filter =>
            {
                filter.SetDescription("Filter an index to packages with Spectre evidence.");
                filter.AddCommand<CatalogFilterCliFxCommand>("clifx").WithDescription("Filter an index to packages with CliFx evidence.");
                filter.AddCommand<CatalogFilterSpectreConsoleCommand>("spectre-console").WithDescription("Filter an index to packages with Spectre.Console evidence.");
                filter.AddCommand<CatalogFilterSpectreConsoleCliCommand>("spectre-console-cli").WithDescription("Filter an index to packages with Spectre.Console.Cli evidence.");
            });
        });

        config.AddBranch("queue", queue =>
        {
            queue.SetDescription("Build CI queue and batch plan artifacts.");
            queue.AddCommand<QueueBackfillIndexedMetadataCommand>("backfill-indexed-metadata").WithDescription("Build a queue of missing indexed package history versions.");
            queue.AddCommand<QueueDispatchPlanCommand>("dispatch-plan").WithDescription("Split a queue into workflow dispatch batches.");
            queue.AddCommand<QueueUntrustedBatchPlanCommand>("untrusted-batch-plan").WithDescription("Select and enrich a queue slice for untrusted analysis.");
        });

        config.AddBranch("analysis", analysis =>
        {
            analysis.SetDescription("Run sandboxed package analysis.");
            analysis.AddCommand<AnalysisRunHelpBatchCommand>("run-help-batch").WithDescription("Run generic help analysis for a plan and emit a promotion-ready expected.json batch.");
            analysis.AddCommand<AnalysisRunHelpCommand>("run-help").WithDescription("Install a tool, crawl `--help`, and synthesize OpenCLI from generic help output.");
            analysis.AddCommand<AnalysisRunCliFxCommand>("run-clifx").WithDescription("Install a CliFx-based tool and synthesize OpenCLI from recursive help crawl.");
            analysis.AddCommand<AnalysisRunUntrustedCommand>("run-untrusted").WithDescription("Install a package in an isolated sandbox and capture OpenCLI/XMLDoc outputs.");
        });

        config.AddBranch("docs", docs =>
        {
            docs.SetDescription("Generate derived discovery documentation artifacts.");
            docs.AddCommand<DocsRebuildIndexesCommand>("rebuild-indexes").WithDescription("Rebuild package summaries, index/all.json, and index/index.json from indexed metadata.");
            docs.AddCommand<DocsBrowserIndexCommand>("browser-index").WithDescription("Build the lightweight browser index from index/all.json.");
            docs.AddCommand<DocsFullyIndexedReportCommand>("fully-indexed-report").WithDescription("Build the fully indexed package documentation coverage report.");
        });

        config.AddBranch("promotion", promotion =>
        {
            promotion.SetDescription("Apply promoted outputs and generate release notes.");
            promotion.AddCommand<PromotionApplyUntrustedCommand>("apply-untrusted").WithDescription("Apply downloaded untrusted analysis artifacts into the repository index and state.");
            promotion.AddCommand<PromotionWriteNotesCommand>("write-notes").WithDescription("Write promotion notes from a promotion summary JSON file.");
        });
    });

    return await app.RunAsync(args);
}
catch (OperationCanceledException)
{
    return await output.WriteErrorAsync("canceled", "Operation canceled.", 10, jsonRequested, ToolRuntime.CancellationToken);
}
catch (FileNotFoundException ex)
{
    return await output.WriteErrorAsync("not-found", ex.Message, 5, jsonRequested, ToolRuntime.CancellationToken);
}
catch (Exception ex)
{
    return await output.WriteErrorAsync("error", ex.Message, 1, jsonRequested, ToolRuntime.CancellationToken, ex);
}
