namespace InSpectra.Discovery.Tool.Queue.Commands;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.Queue.Backfill;

using InSpectra.Discovery.Tool.Infrastructure.Settings;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class QueueBackfillIndexedMetadataCommand : AsyncCommand<QueueBackfillIndexedMetadataCommand.Settings>
{
    private readonly QueueBackfillCommandService _service;

    public QueueBackfillIndexedMetadataCommand(QueueBackfillCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--index <PATH>")]
        [DefaultValue("index/all.json")]
        public string IndexPath { get; set; } = "index/all.json";

        [CommandOption("--output <PATH>")]
        public string OutputPath { get; set; } = string.Empty;

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(OutputPath)
                ? ValidationResult.Error("`--output` is required.")
                : ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.BuildIndexedMetadataBackfillQueueAsync(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot(),
            settings.IndexPath,
            settings.OutputPath,
            settings.Json,
            cancellationToken);
}


