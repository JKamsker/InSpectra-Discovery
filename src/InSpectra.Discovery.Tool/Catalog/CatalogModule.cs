namespace InSpectra.Discovery.Tool.Catalog;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

internal static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddTransient<CatalogCommandService>();
        services.AddTransient<CatalogBuildCommand>();
        services.AddTransient<CatalogDeltaDiscoverCommand>();
        services.AddTransient<CatalogDeltaQueueAllToolsCommand>();
        services.AddTransient<CatalogDeltaQueueSpectreCliCommand>();
        services.AddTransient<CatalogFilterCliFxCommand>();
        services.AddTransient<CatalogFilterSpectreConsoleCommand>();
        services.AddTransient<CatalogFilterSpectreConsoleCliCommand>();

        return services;
    }

    public static void RegisterCommands(IConfigurator config)
    {
        config.AddBranch("catalog", catalog =>
        {
            catalog.SetDescription("NuGet discovery and filtering.");
            catalog.AddCommand<CatalogBuildCommand>("build").WithDescription("Build the current ranked dotnet-tool index from NuGet.");

            catalog.AddBranch("delta", delta =>
            {
                delta.SetDescription("Incremental catalog discovery and scheduled queue generation.");
                delta.AddCommand<CatalogDeltaDiscoverCommand>("discover").WithDescription("Discover added or updated dotnet tools since the saved catalog cursor.");
                delta.AddCommand<CatalogDeltaQueueAllToolsCommand>("queue-all-tools").WithDescription("Queue all changed current dotnet tools for scheduled analysis.");
                delta.AddCommand<CatalogDeltaQueueSpectreCliCommand>("queue-spectre-cli").WithDescription("Narrow the latest delta to Spectre.Console.Cli evidence and emit a queue.");
            });

            catalog.AddBranch("filter", filter =>
            {
                filter.SetDescription("Filter an index to packages with framework evidence.");
                filter.AddCommand<CatalogFilterCliFxCommand>("clifx").WithDescription("Filter an index to packages with CliFx evidence.");
                filter.AddCommand<CatalogFilterSpectreConsoleCommand>("spectre-console").WithDescription("Filter an index to packages with Spectre.Console evidence.");
                filter.AddCommand<CatalogFilterSpectreConsoleCliCommand>("spectre-console-cli").WithDescription("Filter an index to packages with Spectre.Console.Cli evidence.");
            });
        });
    }
}


