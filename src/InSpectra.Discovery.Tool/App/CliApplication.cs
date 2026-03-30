namespace InSpectra.Discovery.Tool.App;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using System.Reflection;

internal static class CliApplication
{
    public static CommandApp Create()
    {
        var services = new ServiceCollection();
        services.AddDiscoveryCli();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.PropagateExceptions();
            config.SetApplicationName("inspectra-discovery");
            config.SetApplicationVersion(Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0");

            CatalogModule.RegisterCommands(config);
            QueueModule.RegisterCommands(config);
            AnalysisModule.RegisterCommands(config);
            DocsModule.RegisterCommands(config);
            PromotionModule.RegisterCommands(config);
        });

        return app;
    }
}


