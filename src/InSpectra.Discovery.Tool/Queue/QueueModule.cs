namespace InSpectra.Discovery.Tool.Queue;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

internal static class QueueModule
{
    public static IServiceCollection AddQueueModule(this IServiceCollection services)
    {
        services.AddTransient<QueueCommandService>();
        services.AddTransient<QueueBackfillCommandService>();
        services.AddTransient<QueueBackfillIndexedMetadataCommand>();
        services.AddTransient<QueueBackfillCurrentAnalysisCommand>();
        services.AddTransient<QueueBackfillLegacyTerminalNegativeCommand>();
        services.AddTransient<QueueDispatchPlanCommand>();
        services.AddTransient<QueueUntrustedBatchPlanCommand>();

        return services;
    }

    public static void RegisterCommands(IConfigurator config)
    {
        config.AddBranch("queue", queue =>
        {
            queue.SetDescription("Build CI queue and batch plan artifacts.");
            queue.AddCommand<QueueBackfillIndexedMetadataCommand>("backfill-indexed-metadata").WithDescription("Build a queue of missing indexed package history versions.");
            queue.AddCommand<QueueBackfillCurrentAnalysisCommand>("backfill-current-analysis").WithDescription("Build a ranked queue of current latest tool versions that still need usable analysis.");
            queue.AddCommand<QueueBackfillLegacyTerminalNegativeCommand>("backfill-legacy-terminal-negative").WithDescription("Build a queue of current packages still marked terminal-negative by the legacy Spectre-only analyzer.");
            queue.AddCommand<QueueDispatchPlanCommand>("dispatch-plan").WithDescription("Split a queue into workflow dispatch batches.");
            queue.AddCommand<QueueUntrustedBatchPlanCommand>("untrusted-batch-plan").WithDescription("Select and enrich a queue slice for untrusted analysis.");
        });
    }
}


