namespace InSpectra.Discovery.Tool.Queue;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class QueueBackfillCurrentAnalysisCommand : AsyncCommand<QueueBackfillCurrentAnalysisCommand.Settings>
{
    private readonly QueueBackfillCommandService _service;

    public QueueBackfillCurrentAnalysisCommand(QueueBackfillCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--current-snapshot <PATH>")]
        [DefaultValue(IndexDeltaOptions.DefaultCurrentSnapshotPath)]
        public string CurrentSnapshotPath { get; set; } = IndexDeltaOptions.DefaultCurrentSnapshotPath;

        [CommandOption("--output <PATH>")]
        public string OutputPath { get; set; } = string.Empty;

        [CommandOption("--take <NUMBER>")]
        public int? Take { get; set; }

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(OutputPath) || Take <= 0
                ? ValidationResult.Error("`--output` and positive `--take` when provided are required.")
                : ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.BuildCurrentAnalysisBackfillQueueAsync(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot(),
            settings.CurrentSnapshotPath,
            settings.OutputPath,
            settings.Take,
            settings.Json,
            cancellationToken);
}


