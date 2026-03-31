namespace InSpectra.Discovery.Tool.Catalog;

using InSpectra.Discovery.Tool.Catalog.Filtering.SpectreConsole;

using InSpectra.Discovery.Tool.Infrastructure.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class CatalogFilterSpectreConsoleCliCommand : AsyncCommand<CatalogFilterSpectreConsoleCliCommand.Settings>
{
    private readonly CatalogCommandService _service;

    public CatalogFilterSpectreConsoleCliCommand(CatalogCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--input <PATH>")]
        [DefaultValue(SpectreConsoleFilterOptions.DefaultInputPath)]
        public string InputPath { get; set; } = SpectreConsoleFilterOptions.DefaultInputPath;

        [CommandOption("--output <PATH>")]
        [DefaultValue(SpectreConsoleFilterOptions.DefaultSpectreConsoleCliOutputPath)]
        public string OutputPath { get; set; } = SpectreConsoleFilterOptions.DefaultSpectreConsoleCliOutputPath;

        [CommandOption("--concurrency <NUMBER>")]
        [DefaultValue(16)]
        public int Concurrency { get; set; } = 16;

        public override ValidationResult Validate()
            => Concurrency > 0
                ? ValidationResult.Success()
                : ValidationResult.Error("`--concurrency` must be a positive integer.");
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RunFilterAsync(
            new SpectreConsoleFilterOptions
            {
                Json = settings.Json,
                Mode = SpectreConsoleFilterMode.SpectreConsoleCliOnly,
                InputPath = settings.InputPath,
                OutputPath = settings.OutputPath,
                Concurrency = settings.Concurrency,
            },
            cancellationToken);
}


