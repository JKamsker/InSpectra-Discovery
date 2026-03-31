namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.App.Composition;

using InSpectra.Discovery.Tool.App;
using Xunit;

public sealed class CliApplicationTests
{
    public static TheoryData<string[]> BranchHelpCases()
        => new()
        {
            new[] { "--help" },
            new[] { "catalog", "--help" },
            new[] { "catalog", "delta", "--help" },
            new[] { "catalog", "filter", "--help" },
            new[] { "queue", "--help" },
            new[] { "analysis", "--help" },
            new[] { "docs", "--help" },
            new[] { "promotion", "--help" },
        };

    public static TheoryData<string[]> CommandHelpCases()
        => new()
        {
            new[] { "catalog", "build", "--help" },
            new[] { "catalog", "delta", "discover", "--help" },
            new[] { "catalog", "delta", "queue-all-tools", "--help" },
            new[] { "catalog", "delta", "queue-spectre-cli", "--help" },
            new[] { "catalog", "filter", "clifx", "--help" },
            new[] { "catalog", "filter", "spectre-console", "--help" },
            new[] { "catalog", "filter", "spectre-console-cli", "--help" },
            new[] { "queue", "backfill-indexed-metadata", "--help" },
            new[] { "queue", "backfill-current-analysis", "--help" },
            new[] { "queue", "backfill-legacy-terminal-negative", "--help" },
            new[] { "queue", "dispatch-plan", "--help" },
            new[] { "queue", "untrusted-batch-plan", "--help" },
            new[] { "analysis", "run-auto", "--help" },
            new[] { "analysis", "run-help-batch", "--help" },
            new[] { "analysis", "run-help", "--help" },
            new[] { "analysis", "run-clifx", "--help" },
            new[] { "analysis", "run-static", "--help" },
            new[] { "analysis", "run-untrusted", "--help" },
            new[] { "analysis", "run-hook", "--help" },
            new[] { "docs", "rebuild-indexes", "--help" },
            new[] { "docs", "regenerate-native-opencli", "--help" },
            new[] { "docs", "regenerate-static-crawls", "--help" },
            new[] { "docs", "regenerate-clifx-crawls", "--help" },
            new[] { "docs", "regenerate-help-crawls", "--help" },
            new[] { "docs", "regenerate-xmldoc-opencli", "--help" },
            new[] { "docs", "browser-index", "--help" },
            new[] { "docs", "fully-indexed-report", "--help" },
            new[] { "promotion", "apply-untrusted", "--help" },
            new[] { "promotion", "write-notes", "--help" },
        };

    [Theory]
    [MemberData(nameof(BranchHelpCases))]
    public async Task Create_Runs_Help_For_Each_Registered_Branch(string[] args)
    {
        var exitCode = await CliApplication.Create().RunAsync(args);

        Assert.Equal(0, exitCode);
    }

    [Theory]
    [MemberData(nameof(CommandHelpCases))]
    public async Task Create_Runs_Help_For_Each_Registered_Command(string[] args)
    {
        var exitCode = await CliApplication.Create().RunAsync(args);

        Assert.Equal(0, exitCode);
    }
}
