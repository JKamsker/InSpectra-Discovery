namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Promotion.Services;

using System.Text.Json.Nodes;
using Xunit;

[Collection("PromotionApplyCommandService")]
public sealed class PromotionApplyNoOpStabilityTests
{
    [Fact]
    public async Task ApplyUntrustedAsync_PreservesIndexedMetadataAndIndexTimestamps_WhenPromotionIsASemanticNoOp()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var previousRepositoryRoot = Environment.GetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT");
        Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", repositoryRoot);

        try
        {
            SeedDiscoveryState(repositoryRoot);
            SeedPromotionDownload(
                repositoryRoot,
                batchId: "seed-batch",
                source: "seed-success",
                attempt: 1,
                analyzedAt: "2026-04-02T01:00:00Z",
                totalMs: 100,
                openCliMs: 25);

            var service = new PromotionApplyCommandService();
            var firstExitCode = await service.ApplyUntrustedAsync(
                Path.Combine(repositoryRoot, "downloads"),
                summaryOutputPath: null,
                json: true,
                CancellationToken.None);

            Assert.Equal(0, firstExitCode);

            var originalMetadata = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "stable.tool", "1.0.0", "metadata.json"));
            var originalAllIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "all.json"));
            var originalBrowserIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "index.json"));
            var originalMinBrowserIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "index.min.json"));
            var originalState = ParseJsonObject(Path.Combine(repositoryRoot, "state", "packages", "stable.tool", "1.0.0.json"));

            SeedPromotionDownload(
                repositoryRoot,
                batchId: "fresh-batch",
                source: "fresh-analysis",
                attempt: 9,
                analyzedAt: "2026-04-03T01:00:00Z",
                totalMs: 999,
                openCliMs: 777);

            var exitCode = await service.ApplyUntrustedAsync(
                Path.Combine(repositoryRoot, "downloads"),
                summaryOutputPath: null,
                json: true,
                CancellationToken.None);

            Assert.Equal(0, exitCode);

            var promotedMetadata = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "stable.tool", "1.0.0", "metadata.json"));
            Assert.Equal(originalMetadata["source"]?.GetValue<string>(), promotedMetadata["source"]?.GetValue<string>());
            Assert.Equal(originalMetadata["batchId"]?.GetValue<string>(), promotedMetadata["batchId"]?.GetValue<string>());
            Assert.Equal(originalMetadata["attempt"]?.GetValue<int>(), promotedMetadata["attempt"]?.GetValue<int>());
            Assert.Equal(originalMetadata["evaluatedAt"]?.GetValue<string>(), promotedMetadata["evaluatedAt"]?.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(originalMetadata["timings"], promotedMetadata["timings"]));

            var promotedAllIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "all.json"));
            Assert.Equal(originalAllIndex["updatedAt"]?.GetValue<string>(), promotedAllIndex["updatedAt"]?.GetValue<string>());
            Assert.Equal(originalAllIndex["generatedAt"]?.GetValue<string>(), promotedAllIndex["generatedAt"]?.GetValue<string>());

            var promotedBrowserIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "index.json"));
            Assert.Equal(originalBrowserIndex["updatedAt"]?.GetValue<string>(), promotedBrowserIndex["updatedAt"]?.GetValue<string>());
            Assert.Equal(originalBrowserIndex["generatedAt"]?.GetValue<string>(), promotedBrowserIndex["generatedAt"]?.GetValue<string>());

            var promotedMinBrowserIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "index.min.json"));
            Assert.Equal(originalMinBrowserIndex["updatedAt"]?.GetValue<string>(), promotedMinBrowserIndex["updatedAt"]?.GetValue<string>());
            Assert.Equal(originalMinBrowserIndex["generatedAt"]?.GetValue<string>(), promotedMinBrowserIndex["generatedAt"]?.GetValue<string>());

            var promotedState = ParseJsonObject(Path.Combine(repositoryRoot, "state", "packages", "stable.tool", "1.0.0.json"));
            Assert.True(JsonNode.DeepEquals(originalState, promotedState));
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    private static void SeedDiscoveryState(string repositoryRoot)
    {
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json"),
            new JsonObject
            {
                ["generatedAtUtc"] = "2026-04-02T00:00:00Z",
                ["packageType"] = "DotnetTool",
                ["packageCount"] = 1,
                ["packages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Stable.Tool",
                        ["latestVersion"] = "1.0.0",
                        ["totalDownloads"] = 42,
                        ["projectUrl"] = "https://example.test/stable",
                    },
                },
            });
    }

    private static void SeedPromotionDownload(
        string repositoryRoot,
        string batchId,
        string source,
        int attempt,
        string analyzedAt,
        int totalMs,
        int openCliMs)
    {
        var downloadRoot = Path.Combine(repositoryRoot, "downloads");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(downloadRoot, "plan", "expected.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["batchId"] = batchId,
                ["targetBranch"] = "main",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Stable.Tool",
                        ["version"] = "1.0.0",
                        ["attempt"] = attempt,
                    },
                },
            });

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(downloadRoot, "analysis-stable-tool", "result.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Stable.Tool",
                ["version"] = "1.0.0",
                ["batchId"] = batchId,
                ["attempt"] = attempt,
                ["trusted"] = false,
                ["source"] = source,
                ["cliFramework"] = "System.CommandLine",
                ["analysisMode"] = "native",
                ["analysisSelection"] = new JsonObject
                {
                    ["preferredMode"] = "native",
                    ["selectedMode"] = "native",
                },
                ["analyzedAt"] = analyzedAt,
                ["disposition"] = "success",
                ["packageUrl"] = "https://www.nuget.org/packages/Stable.Tool/1.0.0",
                ["totalDownloads"] = 42,
                ["packageContentUrl"] = "https://nuget.test/stable.tool.1.0.0.nupkg",
                ["registrationLeafUrl"] = "https://nuget.test/registration/stable.tool/1.0.0.json",
                ["catalogEntryUrl"] = "https://nuget.test/catalog/stable.tool.1.0.0.json",
                ["projectUrl"] = "https://example.test/stable",
                ["sourceRepositoryUrl"] = "https://github.com/example/stable.tool",
                ["publishedAt"] = "2026-04-01T00:00:00Z",
                ["command"] = "stable",
                ["entryPoint"] = "stable.dll",
                ["runner"] = "dotnet",
                ["toolSettingsPath"] = "tools/net10.0/any/DotnetToolSettings.xml",
                ["introspection"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["status"] = "ok",
                        ["classification"] = "json-ready",
                    },
                },
                ["timings"] = new JsonObject
                {
                    ["totalMs"] = totalMs,
                    ["opencliMs"] = openCliMs,
                },
                ["steps"] = new JsonObject
                {
                    ["install"] = new JsonObject
                    {
                        ["status"] = "ok",
                    },
                    ["opencli"] = new JsonObject
                    {
                        ["status"] = "ok",
                        ["classification"] = "json-ready",
                    },
                    ["xmldoc"] = null,
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliArtifact"] = "opencli.json",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(downloadRoot, "analysis-stable-tool", "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "stable",
                },
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
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

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
