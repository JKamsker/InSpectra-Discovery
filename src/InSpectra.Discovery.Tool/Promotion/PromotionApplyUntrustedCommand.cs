namespace InSpectra.Discovery.Tool.Promotion;

using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class PromotionApplyUntrustedCommand : AsyncCommand<PromotionApplyUntrustedCommand.Settings>
{
    private readonly PromotionApplyCommandService _service;

    public PromotionApplyUntrustedCommand(PromotionApplyCommandService service)
    {
        _service = service;
    }

    public sealed class Settings : GlobalSettings
    {
        [CommandOption("--download-root <PATH>")]
        public string DownloadRoot { get; set; } = string.Empty;

        [CommandOption("--summary-output <PATH>")]
        public string SummaryOutputPath { get; set; } = string.Empty;

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(DownloadRoot)
                ? ValidationResult.Error("`--download-root` is required.")
                : ValidationResult.Success();
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _service.ApplyUntrustedAsync(
            settings.DownloadRoot,
            string.IsNullOrWhiteSpace(settings.SummaryOutputPath) ? null : settings.SummaryOutputPath,
            settings.Json,
            cancellationToken);
}


