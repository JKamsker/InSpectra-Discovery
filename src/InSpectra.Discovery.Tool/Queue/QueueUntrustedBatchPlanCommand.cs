namespace InSpectra.Discovery.Tool.Queue;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class QueueUntrustedBatchPlanCommand : AsyncCommand<QueueUntrustedBatchPlanCommand.Settings>
{
    private readonly QueueCommandService _service;

    public QueueUntrustedBatchPlanCommand(QueueCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--queue <PATH>")]
        public string QueuePath { get; set; } = string.Empty;

        [CommandOption("--batch-id <ID>")]
        public string BatchId { get; set; } = string.Empty;

        [CommandOption("--output <PATH>")]
        public string OutputPath { get; set; } = string.Empty;

        [CommandOption("--offset <NUMBER>")]
        [DefaultValue(0)]
        public int Offset { get; set; }

        [CommandOption("--take <NUMBER>")]
        public int? Take { get; set; }

        [CommandOption("--force-reanalyze")]
        public bool ForceReanalyze { get; set; }

        [CommandOption("--target-branch <NAME>")]
        [DefaultValue("main")]
        public string TargetBranch { get; set; } = "main";

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(QueuePath) || string.IsNullOrWhiteSpace(BatchId) || string.IsNullOrWhiteSpace(OutputPath) || Offset < 0 || Take <= 0
                ? ValidationResult.Error("`--queue`, `--batch-id`, `--output`, non-negative `--offset`, and positive `--take` when provided are required.")
                : ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.BuildUntrustedBatchPlanAsync(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot(),
            settings.QueuePath,
            settings.BatchId,
            settings.OutputPath,
            settings.Offset,
            settings.Take,
            settings.ForceReanalyze,
            settings.TargetBranch,
            settings.Json,
            cancellationToken);
}


