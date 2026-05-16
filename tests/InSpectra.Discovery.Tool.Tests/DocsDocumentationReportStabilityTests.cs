namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Docs.Services;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class DocsDocumentationReportStabilityTests
{
    [Fact]
    public async Task BuildFullyIndexedDocumentationReportAsync_PreservesGeneratedLine_WhenReportContentIsOtherwiseUnchanged()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "all.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Stable.Tool",
                        ["latestPaths"] = new JsonObject
                        {
                            ["metadataPath"] = "index/packages/stable.tool/latest/metadata.json",
                            ["opencliPath"] = "index/packages/stable.tool/latest/opencli.json",
                        },
                    },
                },
            });

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "stable.tool", "latest", "metadata.json"),
            new JsonObject
            {
                ["packageId"] = "Stable.Tool",
                ["version"] = "1.0.0",
                ["status"] = "ok",
                ["introspection"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["classification"] = "json-ready",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/stable.tool/latest/opencli.json",
                    ["opencliSource"] = "tool-output",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "stable.tool", "latest", "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "tool-output",
                },
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--verbose",
                        ["description"] = "Verbose output.",
                    },
                },
                ["commands"] = new JsonArray(),
            });

        var reportPath = Path.Combine(repositoryRoot, "docs", "report.md");
        RepositoryPathResolver.WriteLines(
            reportPath,
            new[]
            {
                "# Fully Indexed Package Documentation Report",
                string.Empty,
                "Generated: 2000-01-01 00:00:00+00:00",
                string.Empty,
                "Scope: latest package entries with status ok, whose OpenCLI classification is json-ready or json-ready-with-nonzero-exit, and whose resolved OpenCLI provenance is tool-output.",
                string.Empty,
                "Completeness rule: visible commands, options, and arguments must all have non-empty descriptions, and every visible leaf command must have at least one non-empty example.",
                string.Empty,
                "Hidden commands, options, and arguments are excluded from the score.",
                string.Empty,
                "Packages in scope: 1",
                string.Empty,
                "Fully documented: 1",
                string.Empty,
                "Incomplete: 0",
                string.Empty,
                "| Package | Version | Status | XML | Cmd Docs | Opt Docs | Arg Docs | Leaf Examples | Overall |",
                "| --- | --- | --- | --- | --- | --- | --- | --- | --- |",
                "| [Stable.Tool](#pkg-stable-tool) | 1.0.0 | ok | n/a | 0/0 | 1/1 | 0/0 | 0/0 | PASS |",
                string.Empty,
                "## Package Details",
                string.Empty,
                "<a id=\"pkg-stable-tool\"></a>",
                "### Stable.Tool",
                string.Empty,
                "- Version: `1.0.0`",
                "- Package status: `ok`",
                "- OpenCLI classification: `json-ready`",
                "- XMLDoc classification: `n/a`",
                "- Command documentation: `0/0`",
                "- Option documentation: `1/1`",
                "- Argument documentation: `0/0`",
                "- Leaf command examples: `0/0`",
                "- Overall: `PASS`",
                "- Missing command descriptions: None",
                "- Missing option descriptions: None",
                "- Missing argument descriptions: None",
                "- Missing leaf command examples: None",
            });

        var service = new DocsCommandService();
        var exitCode = await service.BuildFullyIndexedDocumentationReportAsync(
            repositoryRoot,
            "index/all.json",
            "docs/report.md",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var report = File.ReadAllText(reportPath);
        Assert.Contains("Generated: 2000-01-01 00:00:00+00:00", report, StringComparison.Ordinal);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"inspectra-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
