using System.Text.Json.Nodes;
using Xunit;

public sealed class DocsCommandServiceTests
{
    [Fact]
    public async Task RebuildIndexesAsync_ProjectsPackageLinksIntoSummaries()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json"),
            new JsonObject
            {
                ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                ["packageType"] = "DotnetTool",
                ["packageCount"] = 1,
                ["packages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["latestVersion"] = "1.2.3",
                        ["totalDownloads"] = 1234,
                        ["projectUrl"] = "https://github.com/example/sample.tool",
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3", "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["trusted"] = false,
                ["status"] = "partial",
                ["evaluatedAt"] = "2026-03-27T01:00:00Z",
                ["publishedAt"] = "2026-03-27T00:30:00Z",
                ["command"] = "sample",
                ["timings"] = new JsonObject
                {
                    ["totalMs"] = 100,
                },
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sample.tool/1.2.3/metadata.json",
                    ["opencliPath"] = null,
                    ["opencliSource"] = null,
                    ["xmldocPath"] = null,
                },
            });

        var service = new DocsCommandService();
        var exitCode = await service.RebuildIndexesAsync(
            repositoryRoot,
            writeBrowserIndex: true,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var packageIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "index.json"));
        Assert.Equal(1234L, packageIndex["totalDownloads"]?.GetValue<long>());
        Assert.Equal("https://www.nuget.org/packages/Sample.Tool", packageIndex["links"]?["nuget"]?.GetValue<string>());
        Assert.Equal("https://github.com/example/sample.tool", packageIndex["links"]?["project"]?.GetValue<string>());
        Assert.Equal("https://github.com/example/sample.tool", packageIndex["links"]?["source"]?.GetValue<string>());

        var allIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "all.json"));
        var allIndexPackage = allIndex["packages"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one package in all index.");
        Assert.NotNull(allIndex["createdAt"]?.GetValue<string>());
        Assert.NotNull(allIndex["updatedAt"]?.GetValue<string>());
        Assert.Equal("https://github.com/example/sample.tool", allIndexPackage["links"]?["source"]?.GetValue<string>());

        Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "latest", "metadata.json")));
        var browserIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "index.json"));
        Assert.NotNull(browserIndex["createdAt"]?.GetValue<string>());
        Assert.NotNull(browserIndex["updatedAt"]?.GetValue<string>());
        var browserPackage = browserIndex["packages"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one package in browser index.");
        Assert.Equal("2026-03-27T00:30:00.0000000+00:00", browserPackage["createdAt"]?.GetValue<string>());
        Assert.Equal("2026-03-27T00:30:00.0000000+00:00", browserPackage["updatedAt"]?.GetValue<string>());
    }

    [Fact]
    public async Task BuildBrowserIndexAsync_PreservesTotalDownloadsFromAllIndex()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "all.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["createdAt"] = "2026-03-20T00:00:00Z",
                ["generatedAt"] = "2026-03-27T00:00:00Z",
                ["packageCount"] = 1,
                ["packages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["totalDownloads"] = 1234,
                        ["latestVersion"] = "1.2.3",
                        ["latestStatus"] = "ok",
                        ["commandCount"] = 7,
                        ["commandGroupCount"] = 2,
                        ["versions"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["version"] = "1.2.3",
                                ["command"] = "sample",
                                ["publishedAt"] = "2026-03-26T00:00:00Z",
                                ["evaluatedAt"] = "2026-03-26T00:30:00Z",
                            },
                            new JsonObject
                            {
                                ["version"] = "1.1.0-preview.1",
                                ["command"] = "sample",
                                ["publishedAt"] = "1900-01-01T00:00:00Z",
                                ["evaluatedAt"] = "2026-03-19T00:00:00Z",
                            },
                            new JsonObject
                            {
                                ["version"] = "1.0.0",
                                ["command"] = "sample",
                                ["publishedAt"] = "2026-03-20T00:00:00Z",
                                ["evaluatedAt"] = "2026-03-20T00:30:00Z",
                            },
                        },
                    },
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "index.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["generatedAt"] = "2026-03-21T00:00:00Z",
                ["packageCount"] = 0,
                ["packages"] = new JsonArray(),
            });

        var service = new DocsCommandService();
        var exitCode = await service.BuildBrowserIndexAsync(
            repositoryRoot,
            "index/all.json",
            "index/index.json",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var browserIndex = JsonNode.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "index", "index.json")))?.AsObject()
            ?? throw new InvalidOperationException("Generated browser index was empty.");
        var package = browserIndex["packages"]?.AsArray().OfType<JsonObject>().Single()
            ?? throw new InvalidOperationException("Expected one package in browser index.");

        Assert.Equal(
            DateTimeOffset.Parse("2026-03-21T00:00:00Z"),
            DateTimeOffset.Parse(browserIndex["createdAt"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing createdAt.")));
        Assert.NotNull(browserIndex["updatedAt"]?.GetValue<string>());
        Assert.Equal(1234L, package["totalDownloads"]?.GetValue<long>());
        Assert.Equal(
            DateTimeOffset.Parse("2026-03-19T00:00:00Z"),
            DateTimeOffset.Parse(package["createdAt"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing package createdAt.")));
        Assert.Equal(
            DateTimeOffset.Parse("2026-03-26T00:00:00Z"),
            DateTimeOffset.Parse(package["updatedAt"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing package updatedAt.")));
    }

    [Fact]
    public async Task RebuildIndexesAsync_Rebases_Latest_Metadata_Artifact_Paths_And_Copies_Crawl()
    {
        ToolRuntime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "state", "discovery", "dotnet-tools.current.json"),
            new JsonObject
            {
                ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                ["packageType"] = "DotnetTool",
                ["packageCount"] = 1,
                ["packages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["packageId"] = "Sample.Tool",
                        ["latestVersion"] = "1.2.3",
                    },
                },
            });

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Sample.Tool",
                ["version"] = "1.2.3",
                ["status"] = "ok",
                ["command"] = "sample",
                ["publishedAt"] = "2026-03-27T00:30:00Z",
                ["evaluatedAt"] = "2026-03-27T01:00:00Z",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["path"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    },
                    ["xmldoc"] = new JsonObject
                    {
                        ["path"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["metadataPath"] = "index/packages/sample.tool/1.2.3/metadata.json",
                    ["opencliPath"] = "index/packages/sample.tool/1.2.3/opencli.json",
                    ["opencliSource"] = "crawled-from-help",
                    ["xmldocPath"] = "index/packages/sample.tool/1.2.3/xmldoc.xml",
                    ["crawlPath"] = "index/packages/sample.tool/1.2.3/crawl.json",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-help",
                },
            });
        RepositoryPathResolver.WriteTextFile(Path.Combine(versionRoot, "xmldoc.xml"), "<Model />");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "crawl.json"),
            new JsonObject
            {
                ["documentCount"] = 1,
            });

        var service = new DocsCommandService();
        var exitCode = await service.RebuildIndexesAsync(
            repositoryRoot,
            writeBrowserIndex: true,
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "latest", "crawl.json")));

        var latestMetadata = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "latest", "metadata.json"));
        Assert.Equal("index/packages/sample.tool/latest/metadata.json", latestMetadata["artifacts"]?["metadataPath"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/latest/opencli.json", latestMetadata["artifacts"]?["opencliPath"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/latest/xmldoc.xml", latestMetadata["artifacts"]?["xmldocPath"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/latest/crawl.json", latestMetadata["artifacts"]?["crawlPath"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/latest/opencli.json", latestMetadata["steps"]?["opencli"]?["path"]?.GetValue<string>());
        Assert.Equal("index/packages/sample.tool/latest/xmldoc.xml", latestMetadata["steps"]?["xmldoc"]?["path"]?.GetValue<string>());
    }

    [Fact]
    public async Task BuildFullyIndexedDocumentationReportAsync_Requires_ToolOutput_Provenance()
    {
        ToolRuntime.Initialize();

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
                        ["packageId"] = "ToolOutput.Tool",
                        ["latestPaths"] = new JsonObject
                        {
                            ["metadataPath"] = "index/packages/tooloutput.tool/latest/metadata.json",
                            ["opencliPath"] = "index/packages/tooloutput.tool/latest/opencli.json",
                        },
                    },
                    new JsonObject
                    {
                        ["packageId"] = "Unknown.Tool",
                        ["latestPaths"] = new JsonObject
                        {
                            ["metadataPath"] = "index/packages/unknown.tool/latest/metadata.json",
                            ["opencliPath"] = "index/packages/unknown.tool/latest/opencli.json",
                        },
                    },
                },
            });

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "tooloutput.tool", "latest", "metadata.json"),
            new JsonObject
            {
                ["packageId"] = "ToolOutput.Tool",
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
                    ["opencliPath"] = "index/packages/tooloutput.tool/latest/opencli.json",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "tooloutput.tool", "latest", "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "tool-output",
                },
                ["commands"] = new JsonArray(),
            });

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "unknown.tool", "latest", "metadata.json"),
            new JsonObject
            {
                ["packageId"] = "Unknown.Tool",
                ["version"] = "2.0.0",
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
                    ["opencliPath"] = "index/packages/unknown.tool/latest/opencli.json",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "unknown.tool", "latest", "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray(),
            });

        var service = new DocsCommandService();
        var exitCode = await service.BuildFullyIndexedDocumentationReportAsync(
            repositoryRoot,
            "index/all.json",
            "docs/report.md",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var report = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "report.md"));
        Assert.Contains("Packages in scope: 1", report, StringComparison.Ordinal);
        Assert.Contains("ToolOutput.Tool", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Unknown.Tool", report, StringComparison.Ordinal);
        Assert.Contains("resolved OpenCLI provenance is tool-output", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildFullyIndexedDocumentationReportAsync_Prefers_Latest_OpenCli_Provenance_Over_Stale_Metadata()
    {
        ToolRuntime.Initialize();

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
                        ["packageId"] = "Stale.Tool",
                        ["latestPaths"] = new JsonObject
                        {
                            ["metadataPath"] = "index/packages/stale.tool/latest/metadata.json",
                            ["opencliPath"] = "index/packages/stale.tool/latest/opencli.json",
                        },
                    },
                },
            });

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "stale.tool", "latest", "metadata.json"),
            new JsonObject
            {
                ["packageId"] = "Stale.Tool",
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
                    ["opencliPath"] = "index/packages/stale.tool/1.0.0/opencli.json",
                    ["opencliSource"] = "tool-output",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "stale.tool", "latest", "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "crawled-from-help",
                },
                ["commands"] = new JsonArray(),
            });

        var service = new DocsCommandService();
        var exitCode = await service.BuildFullyIndexedDocumentationReportAsync(
            repositoryRoot,
            "index/all.json",
            "docs/report.md",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var report = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "report.md"));
        Assert.Contains("Packages in scope: 0", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Stale.Tool", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildFullyIndexedDocumentationReportAsync_Falls_Back_To_Versioned_OpenCli_When_Latest_Mirror_Is_Missing()
    {
        ToolRuntime.Initialize();

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
                        ["packageId"] = "Fallback.Tool",
                        ["latestPaths"] = new JsonObject
                        {
                            ["metadataPath"] = "index/packages/fallback.tool/latest/metadata.json",
                            ["opencliPath"] = "index/packages/fallback.tool/latest/opencli.json",
                        },
                    },
                },
            });

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "fallback.tool", "latest", "metadata.json"),
            new JsonObject
            {
                ["packageId"] = "Fallback.Tool",
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
                    ["opencliPath"] = "index/packages/fallback.tool/1.0.0/opencli.json",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "fallback.tool", "1.0.0", "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "tool-output",
                },
                ["commands"] = new JsonArray(),
            });

        var service = new DocsCommandService();
        var exitCode = await service.BuildFullyIndexedDocumentationReportAsync(
            repositoryRoot,
            "index/all.json",
            "docs/report.md",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var report = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "report.md"));
        Assert.Contains("Packages in scope: 1", report, StringComparison.Ordinal);
        Assert.Contains("Fallback.Tool", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildFullyIndexedDocumentationReportAsync_Falls_Back_To_OpenCli_Step_Path_When_Artifacts_Path_Is_Missing()
    {
        ToolRuntime.Initialize();

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
                        ["packageId"] = "StepFallback.Tool",
                        ["latestPaths"] = new JsonObject
                        {
                            ["metadataPath"] = "index/packages/stepfallback.tool/latest/metadata.json",
                            ["opencliPath"] = "index/packages/stepfallback.tool/latest/opencli.json",
                        },
                    },
                },
            });

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "stepfallback.tool", "latest", "metadata.json"),
            new JsonObject
            {
                ["packageId"] = "StepFallback.Tool",
                ["version"] = "1.0.0",
                ["status"] = "ok",
                ["introspection"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["classification"] = "json-ready",
                        ["artifactSource"] = "tool-output",
                    },
                },
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["path"] = "index/packages/stepfallback.tool/1.0.0/opencli.json",
                    },
                },
                ["artifacts"] = new JsonObject(),
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "stepfallback.tool", "1.0.0", "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["commands"] = new JsonArray(),
            });

        var service = new DocsCommandService();
        var exitCode = await service.BuildFullyIndexedDocumentationReportAsync(
            repositoryRoot,
            "index/all.json",
            "docs/report.md",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var report = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "report.md"));
        Assert.Contains("Packages in scope: 1", report, StringComparison.Ordinal);
        Assert.Contains("StepFallback.Tool", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildFullyIndexedDocumentationReportAsync_Includes_JsonReadyWithNonzeroExit_Tool_Output()
    {
        ToolRuntime.Initialize();

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
                        ["packageId"] = "NativeExit.Tool",
                        ["latestPaths"] = new JsonObject
                        {
                            ["metadataPath"] = "index/packages/nativeexit.tool/latest/metadata.json",
                            ["opencliPath"] = "index/packages/nativeexit.tool/latest/opencli.json",
                        },
                    },
                },
            });

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "nativeexit.tool", "latest", "metadata.json"),
            new JsonObject
            {
                ["packageId"] = "NativeExit.Tool",
                ["version"] = "1.0.0",
                ["status"] = "ok",
                ["introspection"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["classification"] = "json-ready-with-nonzero-exit",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliPath"] = "index/packages/nativeexit.tool/latest/opencli.json",
                    ["opencliSource"] = "tool-output",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(repositoryRoot, "index", "packages", "nativeexit.tool", "latest", "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "tool-output",
                },
                ["commands"] = new JsonArray(),
            });

        var service = new DocsCommandService();
        var exitCode = await service.BuildFullyIndexedDocumentationReportAsync(
            repositoryRoot,
            "index/all.json",
            "docs/report.md",
            json: true,
            CancellationToken.None);

        Assert.Equal(0, exitCode);

        var report = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "report.md"));
        Assert.Contains("Packages in scope: 1", report, StringComparison.Ordinal);
        Assert.Contains("NativeExit.Tool", report, StringComparison.Ordinal);
        Assert.Contains("json-ready-with-nonzero-exit", report, StringComparison.Ordinal);
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
