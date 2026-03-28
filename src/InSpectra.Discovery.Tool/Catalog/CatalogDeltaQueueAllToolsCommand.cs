using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class CatalogDeltaQueueAllToolsCommand : AsyncCommand<CatalogDeltaQueueAllToolsCommand.Settings>
{
    private readonly CatalogCommandService _service = new();

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--input <PATH>")]
        [DefaultValue(IndexDeltaAllToolsOptions.DefaultInputDeltaPath)]
        public string InputDeltaPath { get; set; } = IndexDeltaAllToolsOptions.DefaultInputDeltaPath;

        [CommandOption("--output <PATH>")]
        [DefaultValue(IndexDeltaAllToolsOptions.DefaultOutputDeltaPath)]
        public string OutputDeltaPath { get; set; } = IndexDeltaAllToolsOptions.DefaultOutputDeltaPath;

        [CommandOption("--queue-output <PATH>")]
        [DefaultValue(IndexDeltaAllToolsOptions.DefaultQueueOutputPath)]
        public string QueueOutputPath { get; set; } = IndexDeltaAllToolsOptions.DefaultQueueOutputPath;
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.RunDeltaQueueAllToolsAsync(
            new IndexDeltaAllToolsOptions
            {
                Json = settings.Json,
                InputDeltaPath = settings.InputDeltaPath,
                OutputDeltaPath = settings.OutputDeltaPath,
                QueueOutputPath = settings.QueueOutputPath,
            },
            cancellationToken);
}
