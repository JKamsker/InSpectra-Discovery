namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Promotion.Services;

using System.Text.Json.Nodes;
using Xunit;

public sealed class PromotionCommandServiceTests
{
    [Fact]
    public async Task WriteNotesAsync_ListsSuccessfulPackages_IncludingUnchangedPromotions()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var summaryPath = Path.Combine(tempDirectory.Path, "summary.json");
        var outputPath = Path.Combine(tempDirectory.Path, "notes.md");

        RepositoryPathResolver.WriteJsonFile(
            summaryPath,
            new JsonObject
            {
                ["batchId"] = "batch-123",
                ["successCount"] = 3,
                ["terminalNegativeCount"] = 0,
                ["retryableFailureCount"] = 1,
                ["terminalFailureCount"] = 0,
                ["missingCount"] = 0,
                ["successItems"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Created.Tool",
                        ["version"] = "1.0.0",
                        ["change"] = "created",
                    },
                    new JsonObject
                    {
                        ["packageId"] = "Updated.Tool",
                        ["previousVersion"] = "1.0.0",
                        ["version"] = "1.1.0",
                        ["change"] = "updated",
                    },
                    new JsonObject
                    {
                        ["packageId"] = "Stable.Tool",
                        ["version"] = "2.0.0",
                        ["change"] = "unchanged",
                    },
                },
                ["nonSuccessItems"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Broken.Tool",
                        ["version"] = "9.9.9",
                        ["status"] = "retryable-failure",
                        ["classification"] = "missing-runtime",
                    },
                },
            });

        var service = new PromotionCommandService();
        var exitCode = await service.WriteNotesAsync(
            summaryPath,
            "https://github.com/JKamsker/InSpectra-Discovery/actions/runs/1",
            outputPath,
            commentIndexPath: null,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var notes = File.ReadAllText(outputPath);
        Assert.Contains("Successful packages:", notes, StringComparison.Ordinal);
        Assert.Contains("- Created.Tool `1.0.0` (created)", notes, StringComparison.Ordinal);
        Assert.Contains("- Updated.Tool `1.0.0` -> `1.1.0` (updated)", notes, StringComparison.Ordinal);
        Assert.Contains("- Stable.Tool `2.0.0` (unchanged indexed output)", notes, StringComparison.Ordinal);
        Assert.Contains("- Broken.Tool 9.9.9: retryable-failure, missing-runtime", notes, StringComparison.Ordinal);
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
