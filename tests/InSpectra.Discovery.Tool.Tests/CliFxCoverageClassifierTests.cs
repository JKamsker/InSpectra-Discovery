namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CliFxCoverageClassifierTests
{
    [Fact]
    public void Classifies_full_help_when_all_captures_parse()
    {
        var classifier = new CliFxCoverageClassifier();
        var crawl = new CliFxHelpCrawler.CliFxCrawlResult(
            Documents: new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = new("demo", "1.0.0", null, null, ["demo"], [], [], []),
                ["sync"] = new("demo", "1.0.0", null, "Sync", ["demo sync"], [], [], []),
            },
            Captures: new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase),
            CaptureSummaries: new Dictionary<string, CliFxCaptureSummary>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = new(string.Empty, "--help", true, false, 0, "USAGE", null),
                ["sync"] = new("sync", "--help", true, false, 0, "USAGE", null),
            });

        var coverage = classifier.Classify(metadataCommandCount: 2, crawl);

        Assert.Equal("full-help", coverage.HelpCoverageMode);
        Assert.Equal("help-and-metadata", coverage.CommandGraphMode);
        Assert.Equal("compatible", coverage.RuntimeCompatibilityMode);
        Assert.Equal(0, coverage.UnparsedCommandCount);
        Assert.Empty(coverage.RequiredFrameworks);
    }

    [Fact]
    public void Classifies_partial_help_when_some_commands_time_out()
    {
        var classifier = new CliFxCoverageClassifier();
        var crawl = new CliFxHelpCrawler.CliFxCrawlResult(
            Documents: new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = new("demo", "1.0.0", null, null, ["demo"], [], [], []),
            },
            Captures: new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase),
            CaptureSummaries: new Dictionary<string, CliFxCaptureSummary>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = new(string.Empty, "--help", true, false, 0, "USAGE", null),
                ["sync"] = new("sync", "-h", false, true, null, null, null),
            });

        var coverage = classifier.Classify(metadataCommandCount: 2, crawl);

        Assert.Equal("partial-help", coverage.HelpCoverageMode);
        Assert.Equal("metadata-augmented", coverage.CommandGraphMode);
        Assert.Equal("compatible", coverage.RuntimeCompatibilityMode);
        Assert.Equal(new[] { "sync" }, coverage.TimedOutCommands);
        Assert.Equal(new[] { "<root>" }, coverage.ParsedCommands);
    }

    [Fact]
    public void Classifies_metadata_only_runtime_blocked_when_framework_is_missing()
    {
        var classifier = new CliFxCoverageClassifier();
        var crawl = new CliFxHelpCrawler.CliFxCrawlResult(
            Documents: new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase),
            Captures: new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase),
            CaptureSummaries: new Dictionary<string, CliFxCaptureSummary>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = new(
                    string.Empty,
                    "-h",
                    false,
                    false,
                    -2147450730,
                    null,
                    "You must install or update .NET to run this application.\nFramework: 'Microsoft.NETCore.App', version '3.1.0'"),
            });

        var coverage = classifier.Classify(metadataCommandCount: 6, crawl);

        Assert.Equal("metadata-only-runtime-blocked", coverage.HelpCoverageMode);
        Assert.Equal("metadata-only", coverage.CommandGraphMode);
        Assert.Equal("missing-framework", coverage.RuntimeCompatibilityMode);
        Assert.Equal(new[] { "<root>" }, coverage.RuntimeBlockedCommands);
        Assert.Single(coverage.RequiredFrameworks);
        Assert.Equal("Microsoft.NETCore.App", coverage.RequiredFrameworks[0].Name);
        Assert.Equal("3.1.0", coverage.RequiredFrameworks[0].Version);
    }
}

