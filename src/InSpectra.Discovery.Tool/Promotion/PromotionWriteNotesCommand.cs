namespace InSpectra.Discovery.Tool.Promotion;

using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class PromotionWriteNotesCommand : AsyncCommand<PromotionWriteNotesCommand.Settings>
{
    private readonly PromotionCommandService _service;

    public PromotionWriteNotesCommand(PromotionCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--summary <PATH>")]
        public string SummaryPath { get; set; } = string.Empty;

        [CommandOption("--workflow-run-url <URL>")]
        public string WorkflowRunUrl { get; set; } = string.Empty;

        [CommandOption("--output <PATH>")]
        public string BodyOutputPath { get; set; } = string.Empty;

        [CommandOption("--comment-index <PATH>")]
        public string? CommentIndexPath { get; set; }

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(SummaryPath) || string.IsNullOrWhiteSpace(WorkflowRunUrl) || string.IsNullOrWhiteSpace(BodyOutputPath)
                ? ValidationResult.Error("`--summary`, `--workflow-run-url`, and `--output` are required.")
                : ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.WriteNotesAsync(
            settings.SummaryPath,
            settings.WorkflowRunUrl,
            settings.BodyOutputPath,
            settings.CommentIndexPath,
            settings.Json,
            cancellationToken);
}


