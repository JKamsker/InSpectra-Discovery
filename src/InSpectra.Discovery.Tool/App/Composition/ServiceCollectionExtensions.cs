namespace InSpectra.Discovery.Tool.App.Composition;

using InSpectra.Discovery.Tool.Analysis;
using InSpectra.Discovery.Tool.Analysis.Bridge;
using InSpectra.Discovery.Tool.Catalog;
using InSpectra.Discovery.Tool.Docs;
using InSpectra.Discovery.Tool.Promotion;
using InSpectra.Discovery.Tool.Queue;
using InSpectra.Lib.Composition;
using Microsoft.Extensions.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscoveryCli(this IServiceCollection services)
    {
        services.AddInSpectraEngine();

        services.AddCatalogModule();
        services.AddQueueModule();
        services.AddAnalysisModule();
        services.AddDocsModule();
        services.AddPromotionModule();

        return services;
    }
}


