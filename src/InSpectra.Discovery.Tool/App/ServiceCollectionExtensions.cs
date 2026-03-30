namespace InSpectra.Discovery.Tool.App;

using Microsoft.Extensions.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscoveryCli(this IServiceCollection services)
    {
        services.AddCatalogModule();
        services.AddQueueModule();
        services.AddAnalysisModule();
        services.AddDocsModule();
        services.AddPromotionModule();

        return services;
    }
}


