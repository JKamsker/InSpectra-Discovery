namespace InSpectra.Discovery.Tool.Promotion;

using InSpectra.Discovery.Tool.Promotion.Commands;

using InSpectra.Discovery.Tool.Promotion.Services;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

internal static class PromotionModule
{
    public static IServiceCollection AddPromotionModule(this IServiceCollection services)
    {
        services.AddTransient<PromotionCommandService>();
        services.AddTransient<PromotionApplyCommandService>();
        services.AddTransient<PromotionApplyUntrustedCommand>();
        services.AddTransient<PromotionWriteNotesCommand>();

        return services;
    }

    public static void RegisterCommands(IConfigurator config)
    {
        config.AddBranch("promotion", promotion =>
        {
            promotion.SetDescription("Apply promoted outputs and generate release notes.");
            promotion.AddCommand<PromotionApplyUntrustedCommand>("apply-untrusted").WithDescription("Apply downloaded untrusted analysis artifacts into the repository index and state.");
            promotion.AddCommand<PromotionWriteNotesCommand>("write-notes").WithDescription("Write promotion notes from a promotion summary JSON file.");
        });
    }
}


