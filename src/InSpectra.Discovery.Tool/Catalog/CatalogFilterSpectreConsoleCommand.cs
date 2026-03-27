using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class CatalogFilterSpectreConsoleCommand : AsyncCommand<CatalogFilterSpectreConsoleCommand.Settings>
{
    private readonly CatalogCommandService _service = new();

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--input <PATH>")]
        [DefaultValue(SpectreConsoleFilterOptions.DefaultInputPath)]
        public string InputPath { get; set; } = SpectreConsoleFilterOptions.DefaultInputPath;

        [CommandOption("--output <PATH>")]
        [DefaultValue(SpectreConsoleFilterOptions.DefaultSpectreConsoleOutputPath)]
        public string OutputPath { get; set; } = SpectreConsoleFilterOptions.DefaultSpectreConsoleOutputPath;

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
                Mode = SpectreConsoleFilterMode.AnySpectreConsole,
                InputPath = settings.InputPath,
                OutputPath = settings.OutputPath,
                Concurrency = settings.Concurrency,
            },
            cancellationToken);
}
