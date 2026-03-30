namespace InSpectra.Discovery.Tool.Catalog;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class CatalogDeltaQueueSpectreCliCommand : AsyncCommand<CatalogDeltaQueueSpectreCliCommand.Settings>
{
    private readonly CatalogCommandService _service;

    public CatalogDeltaQueueSpectreCliCommand(CatalogCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--input <PATH>")]
        [DefaultValue(IndexDeltaSpectreConsoleCliOptions.DefaultInputDeltaPath)]
        public string InputDeltaPath { get; set; } = IndexDeltaSpectreConsoleCliOptions.DefaultInputDeltaPath;

        [CommandOption("--output <PATH>")]
        [DefaultValue(IndexDeltaSpectreConsoleCliOptions.DefaultOutputDeltaPath)]
        public string OutputDeltaPath { get; set; } = IndexDeltaSpectreConsoleCliOptions.DefaultOutputDeltaPath;

        [CommandOption("--queue-output <PATH>")]
        [DefaultValue(IndexDeltaSpectreConsoleCliOptions.DefaultQueueOutputPath)]
        public string QueueOutputPath { get; set; } = IndexDeltaSpectreConsoleCliOptions.DefaultQueueOutputPath;

        [CommandOption("--concurrency <NUMBER>")]
        [DefaultValue(12)]
        public int Concurrency { get; set; } = 12;

        public override ValidationResult Validate()
            => Concurrency > 0
                ? ValidationResult.Success()
                : ValidationResult.Error("`--concurrency` must be a positive integer.");
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RunDeltaQueueSpectreCliAsync(
            new IndexDeltaSpectreConsoleCliOptions
            {
                Json = settings.Json,
                InputDeltaPath = settings.InputDeltaPath,
                OutputDeltaPath = settings.OutputDeltaPath,
                QueueOutputPath = settings.QueueOutputPath,
                Concurrency = settings.Concurrency,
            },
            cancellationToken);
}


