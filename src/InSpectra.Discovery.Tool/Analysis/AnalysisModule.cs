namespace InSpectra.Discovery.Tool.Analysis;

using InSpectra.Discovery.Tool.Analysis.Bridge;
using InSpectra.Discovery.Tool.Analysis.Help.Commands;

using InSpectra.Discovery.Tool.Analysis.Auto.Commands;

using InSpectra.Discovery.Tool.Analysis.Untrusted;

using InSpectra.Discovery.Tool.Analysis.Hook;

using InSpectra.Discovery.Tool.Analysis.Static;

using InSpectra.Discovery.Tool.Analysis.CliFx;

using InSpectra.Discovery.Tool.Analysis.Help.Batch;

using InSpectra.Discovery.Tool.Analysis.Help.Services;

using InSpectra.Discovery.Tool.Analysis.Auto.Services;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

internal static class AnalysisModule
{
    public static IServiceCollection AddAnalysisModule(this IServiceCollection services)
    {
        services.AddSingleton<LibAnalysisBridge>();
        services.AddTransient<AutoCommandService>();
        services.AddTransient<HelpService>();
        services.AddTransient<HelpBatchCommandService>();
        services.AddTransient<CliFxService>();
        services.AddTransient<StaticService>();
        services.AddTransient<HookService>();
        services.AddTransient<UntrustedCommandService>();
        services.AddTransient<RunAutoCommand>();
        services.AddTransient<RunHelpBatchCommand>();
        services.AddTransient<RunHelpCommand>();
        services.AddTransient<RunCliFxCommand>();
        services.AddTransient<RunStaticCommand>();
        services.AddTransient<RunUntrustedCommand>();
        services.AddTransient<RunHookCommand>();

        return services;
    }

    public static void RegisterCommands(IConfigurator config)
    {
        config.AddBranch("analysis", analysis =>
        {
            analysis.SetDescription("Run sandboxed package analysis.");
            analysis.AddCommand<RunAutoCommand>("run-auto").WithDescription("Prefer native Spectre OpenCLI analysis and fall back to generic help crawl.");
            analysis.AddCommand<RunHelpBatchCommand>("run-help-batch").WithDescription("Run generic help analysis for a plan and emit a promotion-ready expected.json batch.");
            analysis.AddCommand<RunHelpCommand>("run-help").WithDescription("Install a tool, crawl `--help`, and synthesize OpenCLI from generic help output.");
            analysis.AddCommand<RunCliFxCommand>("run-clifx").WithDescription("Install a CliFx-based tool and synthesize OpenCLI from recursive help crawl.");
            analysis.AddCommand<RunStaticCommand>("run-static").WithDescription("Install a tool and synthesize OpenCLI from dnlib static analysis and help crawl.");
            analysis.AddCommand<RunUntrustedCommand>("run-untrusted").WithDescription("Install a package in an isolated sandbox and capture OpenCLI/XMLDoc outputs.");
            analysis.AddCommand<RunHookCommand>("run-hook").WithDescription("Install a System.CommandLine tool and capture its command tree via startup hook interception.");
        });
    }
}


