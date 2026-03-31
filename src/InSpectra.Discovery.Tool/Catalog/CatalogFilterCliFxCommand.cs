namespace InSpectra.Discovery.Tool.Catalog;

using InSpectra.Discovery.Tool.Catalog.Filtering.CliFx;

using InSpectra.Discovery.Tool.Infrastructure.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class CatalogFilterCliFxCommand : AsyncCommand<CatalogFilterCliFxCommand.Settings>
{
    private readonly CatalogCommandService _service;

    public CatalogFilterCliFxCommand(CatalogCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--input <PATH>")]
        [DefaultValue(CliFxFilterOptions.DefaultInputPath)]
        public string InputPath { get; set; } = CliFxFilterOptions.DefaultInputPath;

        [CommandOption("--output <PATH>")]
        [DefaultValue(CliFxFilterOptions.DefaultOutputPath)]
        public string OutputPath { get; set; } = CliFxFilterOptions.DefaultOutputPath;

        [CommandOption("--concurrency <NUMBER>")]
        [DefaultValue(16)]
        public int Concurrency { get; set; } = 16;

        public override ValidationResult Validate()
            => Concurrency > 0
                ? ValidationResult.Success()
                : ValidationResult.Error("`--concurrency` must be a positive integer.");
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RunCliFxFilterAsync(
            new CliFxFilterOptions
            {
                Json = settings.Json,
                InputPath = settings.InputPath,
                OutputPath = settings.OutputPath,
                Concurrency = settings.Concurrency,
            },
            cancellationToken);
}


