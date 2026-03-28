using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class DocsRegenerateHelpCrawlsCommand : AsyncCommand<DocsRegenerateHelpCrawlsCommand.Settings>
{
    private readonly ToolHelpCrawlArtifactRegenerator _regenerator = new();

    public sealed class Settings : GlobalSettings
    {
        public override ValidationResult Validate() => ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var result = _regenerator.RegenerateRepository(
            settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot());
        var output = ToolRuntime.CreateOutput();

        return await output.WriteSuccessAsync(
            new
            {
                scannedCount = result.ScannedCount,
                candidateCount = result.CandidateCount,
                rewrittenCount = result.RewrittenCount,
                unchangedCount = result.UnchangedCount,
                failedCount = result.FailedCount,
                rewrittenItems = result.RewrittenItems,
                failedItems = result.FailedItems,
            },
            [
                new SummaryRow("Version records", result.ScannedCount.ToString()),
                new SummaryRow("Help-crawl artifacts", result.CandidateCount.ToString()),
                new SummaryRow("Rewritten", result.RewrittenCount.ToString()),
                new SummaryRow("Unchanged", result.UnchangedCount.ToString()),
                new SummaryRow("Failed", result.FailedCount.ToString()),
            ],
            settings.Json,
            cancellationToken);
    }
}
