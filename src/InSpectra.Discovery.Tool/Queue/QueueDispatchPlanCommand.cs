namespace InSpectra.Discovery.Tool.Queue;

using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal sealed class QueueDispatchPlanCommand : AsyncCommand<QueueDispatchPlanCommand.Settings>
{
    private readonly QueueCommandService _service;

    public QueueDispatchPlanCommand(QueueCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--queue <PATH>")]
        [DefaultValue("state/discovery/dotnet-tools.all-tools.queue.json")]
        public string QueuePath { get; set; } = "state/discovery/dotnet-tools.all-tools.queue.json";

        [CommandOption("--target-branch <NAME>")]
        public string TargetBranch { get; set; } = string.Empty;

        [CommandOption("--state-branch <NAME>")]
        public string StateBranch { get; set; } = string.Empty;

        [CommandOption("--batch-prefix <NAME>")]
        [DefaultValue("discovery-queue")]
        public string BatchPrefix { get; set; } = "discovery-queue";

        [CommandOption("--batch-size <NUMBER>")]
        [DefaultValue(250)]
        public int BatchSize { get; set; } = 250;

        [CommandOption("--output <PATH>")]
        public string OutputPath { get; set; } = string.Empty;

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(TargetBranch) || string.IsNullOrWhiteSpace(StateBranch) || string.IsNullOrWhiteSpace(OutputPath) || BatchSize <= 0
                ? ValidationResult.Error("`--target-branch`, `--state-branch`, `--output`, and a positive `--batch-size` are required.")
                : ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.BuildDispatchPlanAsync(
            settings.QueuePath,
            settings.TargetBranch,
            settings.StateBranch,
            settings.BatchPrefix,
            settings.BatchSize,
            settings.OutputPath,
            settings.Json,
            cancellationToken);
}


