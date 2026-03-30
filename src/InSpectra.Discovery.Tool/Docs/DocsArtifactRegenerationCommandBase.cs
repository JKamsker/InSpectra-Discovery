using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

internal class DocsArtifactRegenerationSettings : GlobalSettings
{
    [CommandOption("--package-id|--package <PACKAGE_ID>")]
    public string? PackageId { get; set; }

    [CommandOption("--version <VERSION>")]
    public string? Version { get; set; }

    [CommandOption("--skip-index-rebuild")]
    [DefaultValue(false)]
    public bool SkipIndexRebuild { get; set; }

    public ArtifactRegenerationScope Scope => new(PackageId, Version);

    public override ValidationResult Validate()
        => !string.IsNullOrWhiteSpace(Version) && string.IsNullOrWhiteSpace(PackageId)
            ? ValidationResult.Error("`--version` requires `--package-id`.")
            : ValidationResult.Success();
}

internal abstract class DocsArtifactRegenerationCommandBase : AsyncCommand<DocsArtifactRegenerationSettings>
{
    protected abstract string ArtifactLabel { get; }

    protected abstract ArtifactRegenerationRunResult Regenerate(
        string repositoryRoot,
        ArtifactRegenerationScope scope,
        bool rebuildIndexes);

    public override async Task<int> ExecuteAsync(CommandContext context, DocsArtifactRegenerationSettings settings, CancellationToken cancellationToken)
    {
        var repositoryRoot = settings.RepoRoot ?? RepositoryPathResolver.ResolveRepositoryRoot();
        var rebuildIndexes = !settings.SkipIndexRebuild;
        var result = Regenerate(repositoryRoot, settings.Scope, rebuildIndexes);
        var output = ToolRuntime.CreateOutput();

        return await output.WriteSuccessAsync(
            new
            {
                scope = new
                {
                    packageId = settings.Scope.PackageId,
                    version = settings.Scope.Version,
                    display = settings.Scope.DisplayLabel,
                },
                rebuildIndexes,
                scannedCount = result.ScannedCount,
                candidateCount = result.CandidateCount,
                rewrittenCount = result.RewrittenCount,
                unchangedCount = result.UnchangedCount,
                failedCount = result.FailedCount,
                rewrittenItems = result.RewrittenItems,
                failedItems = result.FailedItems,
            },
            [
                new SummaryRow("Scope", settings.Scope.DisplayLabel),
                new SummaryRow("Rebuild indexes", rebuildIndexes ? "yes" : "no"),
                new SummaryRow("Version records", result.ScannedCount.ToString()),
                new SummaryRow(ArtifactLabel, result.CandidateCount.ToString()),
                new SummaryRow("Rewritten", result.RewrittenCount.ToString()),
                new SummaryRow("Unchanged", result.UnchangedCount.ToString()),
                new SummaryRow("Failed", result.FailedCount.ToString()),
            ],
            settings.Json,
            cancellationToken);
    }
}
