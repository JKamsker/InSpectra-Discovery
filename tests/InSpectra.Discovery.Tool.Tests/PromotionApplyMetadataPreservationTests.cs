namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Promotion.Services;

using System.Text.Json.Nodes;
using Xunit;

[Collection("PromotionApplyCommandService")]
public sealed class PromotionApplyMetadataPreservationTests
{
    [Fact]
    public async Task ApplyUntrustedAsync_PreservesStableMetadataFields_WhenFreshSuccessResultOmitsThem()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var previousRepositoryRoot = Environment.GetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT");
        Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", repositoryRoot);

        try
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
                            ["packageId"] = "Preserve.Tool",
                            ["latestVersion"] = "1.0.0",
                            ["totalDownloads"] = 555,
                            ["projectUrl"] = "https://example.test/preserve",
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(repositoryRoot, "index", "packages", "preserve.tool", "1.0.0", "metadata.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Preserve.Tool",
                    ["version"] = "1.0.0",
                    ["trusted"] = false,
                    ["analysisMode"] = "help",
                    ["cliFramework"] = "System.CommandLine",
                    ["source"] = "seed",
                    ["batchId"] = "seed",
                    ["attempt"] = 1,
                    ["status"] = "partial",
                    ["evaluatedAt"] = "2026-04-01T00:00:00Z",
                    ["publishedAt"] = "2026-03-01T00:00:00Z",
                    ["packageUrl"] = "https://www.nuget.org/packages/Preserve.Tool/1.0.0",
                    ["totalDownloads"] = 555,
                    ["packageContentUrl"] = "https://nuget.test/preserve.tool.1.0.0.nupkg",
                    ["registrationLeafUrl"] = "https://nuget.test/registration/preserve.tool/1.0.0.json",
                    ["catalogEntryUrl"] = "https://nuget.test/catalog/preserve.tool.1.0.0.json",
                    ["projectUrl"] = "https://example.test/preserve",
                    ["sourceRepositoryUrl"] = "https://github.com/example/preserve.tool",
                    ["command"] = "preserve",
                    ["entryPoint"] = "preserve.dll",
                    ["runner"] = "dotnet",
                    ["toolSettingsPath"] = "tools/net10.0/any/DotnetToolSettings.xml",
                    ["detection"] = new JsonObject
                    {
                        ["hasSpectreConsole"] = false,
                    },
                    ["introspection"] = new JsonObject
                    {
                        ["opencli"] = null,
                        ["xmldoc"] = null,
                    },
                    ["steps"] = new JsonObject
                    {
                        ["install"] = null,
                        ["opencli"] = null,
                        ["xmldoc"] = null,
                    },
                    ["artifacts"] = new JsonObject
                    {
                        ["metadataPath"] = "index/packages/preserve.tool/1.0.0/metadata.json",
                    },
                });

            var downloadRoot = Path.Combine(repositoryRoot, "downloads");
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-preserve",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Preserve.Tool",
                            ["version"] = "1.0.0",
                            ["attempt"] = 2,
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-preserve-tool", "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Preserve.Tool",
                    ["version"] = "1.0.0",
                    ["batchId"] = "batch-preserve",
                    ["attempt"] = 2,
                    ["trusted"] = false,
                    ["source"] = "reanalysis",
                    ["analysisMode"] = "hook",
                    ["analysisSelection"] = new JsonObject
                    {
                        ["preferredMode"] = "static",
                        ["selectedMode"] = "hook",
                    },
                    ["analyzedAt"] = "2026-04-02T01:00:00Z",
                    ["disposition"] = "success",
                    ["cliFramework"] = null,
                    ["packageUrl"] = null,
                    ["totalDownloads"] = null,
                    ["packageContentUrl"] = null,
                    ["registrationLeafUrl"] = null,
                    ["catalogEntryUrl"] = null,
                    ["projectUrl"] = null,
                    ["sourceRepositoryUrl"] = null,
                    ["command"] = null,
                    ["entryPoint"] = null,
                    ["runner"] = null,
                    ["toolSettingsPath"] = null,
                    ["detection"] = null,
                    ["introspection"] = new JsonObject
                    {
                        ["opencli"] = new JsonObject
                        {
                            ["status"] = "ok",
                            ["classification"] = "startup-hook",
                        },
                    },
                    ["steps"] = new JsonObject
                    {
                        ["install"] = new JsonObject
                        {
                            ["status"] = "ok",
                            ["timedOut"] = false,
                            ["exitCode"] = 0,
                            ["durationMs"] = 10,
                            ["stdout"] = "installed",
                            ["stderr"] = null,
                        },
                        ["opencli"] = new JsonObject
                        {
                            ["status"] = "ok",
                            ["classification"] = "startup-hook",
                        },
                        ["xmldoc"] = null,
                    },
                    ["timings"] = new JsonObject
                    {
                        ["totalMs"] = 20,
                        ["installMs"] = 10,
                        ["crawlMs"] = 0,
                    },
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-preserve-tool", "opencli.json"),
                new JsonObject
                {
                    ["opencli"] = "0.1-draft",
                    ["info"] = new JsonObject
                    {
                        ["title"] = "preserve",
                    },
                    ["x-inspectra"] = new JsonObject
                    {
                        ["artifactSource"] = "startup-hook",
                    },
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "--verbose",
                            ["recursive"] = false,
                            ["hidden"] = false,
                            ["arguments"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["required"] = false,
                                    ["arity"] = new JsonObject
                                    {
                                        ["minimum"] = 0,
                                        ["maximum"] = 0,
                                    },
                                    ["type"] = "Boolean",
                                },
                            },
                        },
                    },
                });

            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, null, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var metadata = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "preserve.tool", "1.0.0", "metadata.json"));
            Assert.Equal(555L, metadata["totalDownloads"]?.GetValue<long>());
            Assert.Equal("https://example.test/preserve", metadata["projectUrl"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/preserve.tool", metadata["sourceRepositoryUrl"]?.GetValue<string>());
            Assert.Equal("preserve", metadata["command"]?.GetValue<string>());
            Assert.Equal("preserve.dll", metadata["entryPoint"]?.GetValue<string>());
            Assert.Equal("dotnet", metadata["runner"]?.GetValue<string>());
            Assert.Equal("tools/net10.0/any/DotnetToolSettings.xml", metadata["toolSettingsPath"]?.GetValue<string>());
            Assert.Equal("System.CommandLine", metadata["cliFramework"]?.GetValue<string>());
            Assert.NotNull(metadata["detection"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "inspectra-promotion-preservation-" + Guid.NewGuid().ToString("N"));
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
