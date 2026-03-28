using System.Text.Json.Nodes;
using Xunit;

public sealed class PromotionApplyCommandServiceTests
{
    [Fact]
    public async Task ApplyUntrustedAsync_ProjectsTotalDownloadsIntoMetadataAndIndexes()
    {
        ToolRuntime.Initialize();

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
                    ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                    ["packageType"] = "DotnetTool",
                    ["packageCount"] = 2,
                    ["source"] = new JsonObject
                    {
                        ["serviceIndexUrl"] = "https://api.nuget.org/v3/index.json",
                        ["autocompleteUrl"] = "https://nuget.test/autocomplete",
                        ["searchUrl"] = "https://nuget.test/search",
                        ["registrationBaseUrl"] = "https://nuget.test/registration/",
                        ["prefixAlphabet"] = "abc",
                        ["expectedPackageCount"] = 2,
                        ["sortOrder"] = "totalDownloads-desc",
                    },
                    ["packages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Existing.Tool",
                            ["latestVersion"] = "1.0.0",
                            ["totalDownloads"] = 4321,
                            ["projectUrl"] = "https://github.com/example/existing.tool",
                        },
                        new JsonObject
                        {
                            ["packageId"] = "Sample.Tool",
                            ["latestVersion"] = "1.2.3",
                            ["totalDownloads"] = 1234,
                            ["projectUrl"] = "https://sample.tool.example",
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(repositoryRoot, "index", "packages", "existing.tool", "1.0.0", "metadata.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Existing.Tool",
                    ["version"] = "1.0.0",
                    ["trusted"] = false,
                    ["source"] = "seed",
                    ["batchId"] = "seed",
                    ["attempt"] = 1,
                    ["status"] = "partial",
                    ["evaluatedAt"] = "2026-03-20T00:00:00Z",
                    ["publishedAt"] = "2026-03-19T00:00:00Z",
                    ["packageUrl"] = "https://www.nuget.org/packages/Existing.Tool/1.0.0",
                    ["packageContentUrl"] = "https://nuget.test/existing.tool.1.0.0.nupkg",
                    ["registrationLeafUrl"] = "https://nuget.test/registration/existing.tool/1.0.0.json",
                    ["catalogEntryUrl"] = "https://nuget.test/catalog/existing.tool.1.0.0.json",
                    ["command"] = "existing",
                    ["entryPoint"] = "existing.dll",
                    ["runner"] = "dotnet",
                    ["toolSettingsPath"] = "tools/net10.0/any/DotnetToolSettings.xml",
                    ["detection"] = new JsonObject(),
                    ["introspection"] = new JsonObject
                    {
                        ["opencli"] = null,
                        ["xmldoc"] = null,
                    },
                    ["timings"] = new JsonObject
                    {
                        ["totalMs"] = 100,
                    },
                    ["steps"] = new JsonObject
                    {
                        ["install"] = null,
                        ["opencli"] = null,
                        ["xmldoc"] = null,
                    },
                    ["artifacts"] = new JsonObject
                    {
                        ["metadataPath"] = "index/packages/existing.tool/1.0.0/metadata.json",
                        ["opencliPath"] = null,
                        ["opencliSource"] = null,
                        ["xmldocPath"] = null,
                    },
                });

            var downloadRoot = Path.Combine(repositoryRoot, "downloads");
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-001",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Sample.Tool",
                            ["version"] = "1.2.3",
                            ["attempt"] = 1,
                            ["cliFramework"] = "System.CommandLine",
                            ["packageUrl"] = "https://www.nuget.org/packages/Sample.Tool/1.2.3",
                            ["packageContentUrl"] = "https://nuget.test/sample.tool.1.2.3.nupkg",
                            ["catalogEntryUrl"] = "https://nuget.test/catalog/sample.tool.1.2.3.json",
                            ["totalDownloads"] = 1234,
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-sample-tool", "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Sample.Tool",
                    ["version"] = "1.2.3",
                    ["batchId"] = "batch-001",
                    ["attempt"] = 1,
                    ["trusted"] = false,
                    ["source"] = "analyze-untrusted-batch",
                    ["cliFramework"] = "System.CommandLine",
                    ["analysisMode"] = "native",
                    ["analysisSelection"] = new JsonObject
                    {
                        ["preferredMode"] = "native",
                        ["selectedMode"] = "native",
                        ["reason"] = "confirmed-spectre-console-cli",
                    },
                    ["analyzedAt"] = "2026-03-27T01:00:00Z",
                    ["disposition"] = "success",
                    ["retryEligible"] = false,
                    ["phase"] = "complete",
                    ["classification"] = "complete",
                    ["failureMessage"] = null,
                    ["failureSignature"] = null,
                    ["packageUrl"] = "https://www.nuget.org/packages/Sample.Tool/1.2.3",
                    ["packageContentUrl"] = "https://nuget.test/sample.tool.1.2.3.nupkg",
                    ["registrationLeafUrl"] = "https://nuget.test/registration/sample.tool/1.2.3.json",
                    ["catalogEntryUrl"] = "https://nuget.test/catalog/sample.tool.1.2.3.json",
                    ["projectUrl"] = "https://sample.tool.example",
                    ["sourceRepositoryUrl"] = "https://github.com/example/sample.tool.git",
                    ["publishedAt"] = "2026-03-27T00:30:00Z",
                    ["command"] = "sample",
                    ["entryPoint"] = "sample.dll",
                    ["runner"] = "dotnet",
                    ["toolSettingsPath"] = "tools/net10.0/any/DotnetToolSettings.xml",
                    ["detection"] = new JsonObject
                    {
                        ["hasSpectreConsole"] = true,
                        ["hasSpectreConsoleCli"] = true,
                        ["matchedPackageEntries"] = new JsonArray(),
                        ["matchedDependencyIds"] = new JsonArray(),
                    },
                    ["introspection"] = new JsonObject
                    {
                        ["opencli"] = new JsonObject
                        {
                            ["status"] = "ok",
                            ["classification"] = "json-ready",
                        },
                        ["xmldoc"] = null,
                    },
                    ["timings"] = new JsonObject
                    {
                        ["totalMs"] = 500,
                        ["installMs"] = 100,
                        ["opencliMs"] = 200,
                        ["xmldocMs"] = null,
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
                        ["crawlArtifact"] = "crawl.json",
                        ["xmldocArtifact"] = null,
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-sample-tool", "opencli.json"),
                new JsonObject
                {
                    ["opencli"] = "0.1-draft",
                    ["info"] = new JsonObject
                    {
                        ["title"] = "sample",
                        ["version"] = "1.0",
                        ["description"] = null,
                    },
                    ["options"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "--verbose",
                            ["required"] = false,
                            ["description"] = null,
                            ["aliases"] = new JsonArray(),
                        },
                    },
                    ["commands"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "serve",
                            ["description"] = null,
                            ["arguments"] = null,
                            ["examples"] = new JsonArray(),
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-sample-tool", "crawl.json"),
                new JsonObject
                {
                    ["documentCount"] = 1,
                    ["captureCount"] = 1,
                    ["commands"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["command"] = "sample",
                            ["parsed"] = true,
                        },
                    },
                });

            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, summaryOutputPath: null, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var sampleMetadata = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3", "metadata.json"));
            Assert.Equal(1234L, sampleMetadata["totalDownloads"]?.GetValue<long>());
            Assert.Equal("ok", sampleMetadata["status"]?.GetValue<string>());
            Assert.Equal("native", sampleMetadata["analysisMode"]?.GetValue<string>());
            Assert.Equal("native", sampleMetadata["analysisSelection"]?["preferredMode"]?.GetValue<string>());
            Assert.Equal("System.CommandLine", sampleMetadata["cliFramework"]?.GetValue<string>());
            Assert.Equal("index/packages/sample.tool/1.2.3/crawl.json", sampleMetadata["artifacts"]?["crawlPath"]?.GetValue<string>());
            Assert.Equal("tool-output", sampleMetadata["artifacts"]?["opencliSource"]?.GetValue<string>());
            Assert.Equal("tool-output", sampleMetadata["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>());

            var sampleCrawl = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3", "crawl.json"));
            Assert.Equal(1, sampleCrawl["captureCount"]?.GetValue<int>());
            Assert.Equal("sample", sampleCrawl["commands"]?[0]?["command"]?.GetValue<string>());

            var sampleOpenCli = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "1.2.3", "opencli.json"));
            Assert.Equal("tool-output", sampleOpenCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
            Assert.False(sampleOpenCli["info"]!.AsObject().ContainsKey("description"));

            var sampleOption = sampleOpenCli["options"]![0]!.AsObject();
            Assert.False(sampleOption.ContainsKey("required"));
            Assert.False(sampleOption.ContainsKey("description"));
            Assert.False(sampleOption.ContainsKey("aliases"));

            var sampleCommand = sampleOpenCli["commands"]![0]!.AsObject();
            Assert.False(sampleCommand.ContainsKey("description"));
            Assert.False(sampleCommand.ContainsKey("arguments"));
            Assert.False(sampleCommand.ContainsKey("examples"));

            var existingPackageIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "existing.tool", "index.json"));
            Assert.Equal(4321L, existingPackageIndex["totalDownloads"]?.GetValue<long>());
            Assert.Equal("https://www.nuget.org/packages/Existing.Tool", existingPackageIndex["links"]?["nuget"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/existing.tool", existingPackageIndex["links"]?["project"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/existing.tool", existingPackageIndex["links"]?["source"]?.GetValue<string>());

            var samplePackageIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "sample.tool", "index.json"));
            Assert.Equal(1234L, samplePackageIndex["totalDownloads"]?.GetValue<long>());
            Assert.Equal("System.CommandLine", samplePackageIndex["cliFramework"]?.GetValue<string>());
            Assert.Equal("https://www.nuget.org/packages/Sample.Tool", samplePackageIndex["links"]?["nuget"]?.GetValue<string>());
            Assert.Equal("https://sample.tool.example", samplePackageIndex["links"]?["project"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/sample.tool", samplePackageIndex["links"]?["source"]?.GetValue<string>());

            var allIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "all.json"));
            Assert.Equal(4321L, FindPackage(allIndex, "Existing.Tool")["totalDownloads"]?.GetValue<long>());
            Assert.Equal(1234L, FindPackage(allIndex, "Sample.Tool")["totalDownloads"]?.GetValue<long>());
            Assert.Equal("System.CommandLine", FindPackage(allIndex, "Sample.Tool")["cliFramework"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/existing.tool", FindPackage(allIndex, "Existing.Tool")["links"]?["source"]?.GetValue<string>());
            Assert.Equal("https://github.com/example/sample.tool", FindPackage(allIndex, "Sample.Tool")["links"]?["source"]?.GetValue<string>());

            var browserIndex = ParseJsonObject(Path.Combine(repositoryRoot, "index", "index.json"));
            Assert.Equal(4321L, FindPackage(browserIndex, "Existing.Tool")["totalDownloads"]?.GetValue<long>());
            Assert.Equal(1234L, FindPackage(browserIndex, "Sample.Tool")["totalDownloads"]?.GetValue<long>());
            Assert.Equal("System.CommandLine", FindPackage(browserIndex, "Sample.Tool")["cliFramework"]?.GetValue<string>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    [Fact]
    public async Task ApplyUntrustedAsync_PreservesHelpDerivedOpenCliProvenance_AndMarksSuccessAsOk()
    {
        ToolRuntime.Initialize();

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
                    ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                    ["packageType"] = "DotnetTool",
                    ["packageCount"] = 1,
                    ["packages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Help.Tool",
                            ["latestVersion"] = "2.0.0",
                            ["totalDownloads"] = 250,
                            ["projectUrl"] = "https://help.example",
                        },
                    },
                });

            var downloadRoot = Path.Combine(repositoryRoot, "downloads");
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-help",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Help.Tool",
                            ["version"] = "2.0.0",
                            ["attempt"] = 1,
                            ["command"] = "help-tool",
                            ["cliFramework"] = "System.CommandLine",
                            ["analysisMode"] = "help",
                            ["packageUrl"] = "https://www.nuget.org/packages/Help.Tool/2.0.0",
                            ["packageContentUrl"] = "https://nuget.test/help.tool.2.0.0.nupkg",
                            ["catalogEntryUrl"] = "https://nuget.test/catalog/help.tool.2.0.0.json",
                            ["totalDownloads"] = 250,
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-help-tool", "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Help.Tool",
                    ["version"] = "2.0.0",
                    ["batchId"] = "batch-help",
                    ["attempt"] = 1,
                    ["source"] = "analyze-untrusted-batch",
                    ["cliFramework"] = "System.CommandLine",
                    ["analysisMode"] = "help",
                    ["analysisSelection"] = new JsonObject
                    {
                        ["preferredMode"] = "help",
                        ["selectedMode"] = "help",
                        ["reason"] = "generic-help-crawl",
                    },
                    ["fallback"] = new JsonObject
                    {
                        ["from"] = "native",
                        ["classification"] = "unsupported-command",
                    },
                    ["analyzedAt"] = "2026-03-27T02:00:00Z",
                    ["disposition"] = "success",
                    ["packageUrl"] = "https://www.nuget.org/packages/Help.Tool/2.0.0",
                    ["packageContentUrl"] = "https://nuget.test/help.tool.2.0.0.nupkg",
                    ["registrationLeafUrl"] = "https://nuget.test/registration/help.tool/2.0.0.json",
                    ["catalogEntryUrl"] = "https://nuget.test/catalog/help.tool.2.0.0.json",
                    ["projectUrl"] = "https://help.example",
                    ["publishedAt"] = "2026-03-27T00:15:00Z",
                    ["totalDownloads"] = 250,
                    ["command"] = "help-tool",
                    ["introspection"] = new JsonObject
                    {
                        ["opencli"] = new JsonObject
                        {
                            ["status"] = "ok",
                        },
                        ["xmldoc"] = null,
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
                            ["classification"] = "help-crawl",
                        },
                        ["xmldoc"] = null,
                    },
                    ["timings"] = new JsonObject
                    {
                        ["totalMs"] = 150,
                        ["crawlMs"] = 75,
                    },
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                        ["crawlArtifact"] = "crawl.json",
                        ["xmldocArtifact"] = null,
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-help-tool", "opencli.json"),
                new JsonObject
                {
                    ["opencli"] = "0.1-draft",
                    ["info"] = new JsonObject
                    {
                        ["title"] = "Help Tool",
                        ["version"] = "2.0.0",
                    },
                    ["x-inspectra"] = new JsonObject
                    {
                        ["artifactSource"] = "crawled-from-help",
                        ["helpDocumentCount"] = 4,
                    },
                    ["commands"] = new JsonArray(),
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-help-tool", "crawl.json"),
                new JsonObject
                {
                    ["documentCount"] = 4,
                    ["captureCount"] = 4,
                    ["commands"] = new JsonArray(),
                });

            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, summaryOutputPath: null, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var metadata = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "help.tool", "2.0.0", "metadata.json"));
            Assert.Equal("ok", metadata["status"]?.GetValue<string>());
            Assert.Equal("help", metadata["analysisMode"]?.GetValue<string>());
            Assert.Equal("help", metadata["analysisSelection"]?["selectedMode"]?.GetValue<string>());
            Assert.Equal("native", metadata["fallback"]?["from"]?.GetValue<string>());
            Assert.Equal("crawled-from-help", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
            Assert.Equal("crawled-from-help", metadata["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>());
            Assert.Equal("crawled-from-help", metadata["introspection"]?["opencli"]?["artifactSource"]?.GetValue<string>());

            var openCli = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "help.tool", "2.0.0", "opencli.json"));
            Assert.Equal("crawled-from-help", openCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    [Fact]
    public async Task ApplyUntrustedAsync_MarksXmldocSynthesizedSuccess_AsOk()
    {
        ToolRuntime.Initialize();

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
                    ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                    ["packageType"] = "DotnetTool",
                    ["packageCount"] = 1,
                    ["packages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Xml.Tool",
                            ["latestVersion"] = "3.0.0",
                            ["totalDownloads"] = 150,
                            ["projectUrl"] = "https://xml.example",
                        },
                    },
                });

            var downloadRoot = Path.Combine(repositoryRoot, "downloads");
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-xml",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Xml.Tool",
                            ["version"] = "3.0.0",
                            ["attempt"] = 1,
                            ["command"] = "xml-tool",
                            ["analysisMode"] = "native",
                            ["packageUrl"] = "https://www.nuget.org/packages/Xml.Tool/3.0.0",
                            ["packageContentUrl"] = "https://nuget.test/xml.tool.3.0.0.nupkg",
                            ["catalogEntryUrl"] = "https://nuget.test/catalog/xml.tool.3.0.0.json",
                            ["totalDownloads"] = 150,
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-xml-tool", "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Xml.Tool",
                    ["version"] = "3.0.0",
                    ["batchId"] = "batch-xml",
                    ["attempt"] = 1,
                    ["source"] = "analyze-untrusted-batch",
                    ["analysisMode"] = "native",
                    ["analyzedAt"] = "2026-03-27T03:00:00Z",
                    ["disposition"] = "success",
                    ["packageUrl"] = "https://www.nuget.org/packages/Xml.Tool/3.0.0",
                    ["packageContentUrl"] = "https://nuget.test/xml.tool.3.0.0.nupkg",
                    ["registrationLeafUrl"] = "https://nuget.test/registration/xml.tool/3.0.0.json",
                    ["catalogEntryUrl"] = "https://nuget.test/catalog/xml.tool.3.0.0.json",
                    ["projectUrl"] = "https://xml.example",
                    ["publishedAt"] = "2026-03-27T00:45:00Z",
                    ["totalDownloads"] = 150,
                    ["command"] = "xml-tool",
                    ["introspection"] = new JsonObject
                    {
                        ["opencli"] = new JsonObject
                        {
                            ["status"] = "missing",
                        },
                        ["xmldoc"] = new JsonObject
                        {
                            ["status"] = "ok",
                        },
                    },
                    ["steps"] = new JsonObject
                    {
                        ["install"] = new JsonObject
                        {
                            ["status"] = "ok",
                        },
                        ["opencli"] = new JsonObject
                        {
                            ["status"] = "missing",
                        },
                        ["xmldoc"] = new JsonObject
                        {
                            ["status"] = "ok",
                        },
                    },
                    ["timings"] = new JsonObject
                    {
                        ["totalMs"] = 175,
                        ["xmldocMs"] = 60,
                    },
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = null,
                        ["xmldocArtifact"] = "xmldoc.xml",
                    },
                });

            RepositoryPathResolver.WriteTextFile(
                Path.Combine(downloadRoot, "analysis-xml-tool", "xmldoc.xml"),
                """
                <Model>
                  <Command Name="serve">
                    <Description>Serve content</Description>
                  </Command>
                </Model>
                """);

            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, summaryOutputPath: null, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var metadata = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "xml.tool", "3.0.0", "metadata.json"));
            Assert.Equal("ok", metadata["status"]?.GetValue<string>());
            Assert.Equal("synthesized-from-xmldoc", metadata["artifacts"]?["opencliSource"]?.GetValue<string>());
            Assert.Equal("synthesized-from-xmldoc", metadata["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>());
            Assert.Equal("synthesized-from-xmldoc", metadata["introspection"]?["opencli"]?["artifactSource"]?.GetValue<string>());
            Assert.True(metadata["introspection"]?["opencli"]?["synthesizedArtifact"]?.GetValue<bool>());

            var openCli = ParseJsonObject(Path.Combine(repositoryRoot, "index", "packages", "xml.tool", "3.0.0", "opencli.json"));
            Assert.Equal("synthesized-from-xmldoc", openCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
            Assert.Equal("3.0.0", openCli["info"]?["version"]?.GetValue<string>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    [Fact]
    public async Task ApplyUntrustedAsync_Rejects_NonObject_OpenCli_Artifacts()
    {
        ToolRuntime.Initialize();

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
                    ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                    ["packageType"] = "DotnetTool",
                    ["packageCount"] = 1,
                    ["packages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Bad.Tool",
                            ["latestVersion"] = "1.0.0",
                            ["totalDownloads"] = 25,
                        },
                    },
                });

            var downloadRoot = Path.Combine(repositoryRoot, "downloads");
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-bad",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Bad.Tool",
                            ["version"] = "1.0.0",
                            ["attempt"] = 1,
                            ["command"] = "bad-tool",
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-bad-tool", "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Bad.Tool",
                    ["version"] = "1.0.0",
                    ["batchId"] = "batch-bad",
                    ["attempt"] = 1,
                    ["source"] = "analyze-untrusted-batch",
                    ["analyzedAt"] = "2026-03-27T04:00:00Z",
                    ["disposition"] = "success",
                    ["command"] = "bad-tool",
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "opencli.json",
                        ["xmldocArtifact"] = null,
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-bad-tool", "opencli.json"),
                new JsonArray
                {
                    "not-an-opencli-object",
                });

            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, summaryOutputPath: null, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var state = ParseJsonObject(Path.Combine(repositoryRoot, "state", "packages", "bad.tool", "1.0.0.json"));
            Assert.Equal("retryable-failure", state["currentStatus"]?.GetValue<string>());
            Assert.False(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "bad.tool", "1.0.0", "metadata.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    [Fact]
    public async Task ApplyUntrustedAsync_Rejects_Artifacts_Outside_The_Result_Directory()
    {
        ToolRuntime.Initialize();

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
                    ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                    ["packageType"] = "DotnetTool",
                    ["packageCount"] = 1,
                    ["packages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Unsafe.Tool",
                            ["latestVersion"] = "1.0.0",
                            ["totalDownloads"] = 15,
                        },
                    },
                });

            var downloadRoot = Path.Combine(repositoryRoot, "downloads");
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-unsafe",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Unsafe.Tool",
                            ["version"] = "1.0.0",
                            ["attempt"] = 1,
                            ["command"] = "unsafe-tool",
                        },
                    },
                });

            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "analysis-unsafe-tool", "result.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["packageId"] = "Unsafe.Tool",
                    ["version"] = "1.0.0",
                    ["batchId"] = "batch-unsafe",
                    ["attempt"] = 1,
                    ["source"] = "analyze-untrusted-batch",
                    ["analyzedAt"] = "2026-03-27T05:00:00Z",
                    ["disposition"] = "success",
                    ["command"] = "unsafe-tool",
                    ["artifacts"] = new JsonObject
                    {
                        ["opencliArtifact"] = "..\\shared\\opencli.json",
                        ["xmldocArtifact"] = null,
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "shared", "opencli.json"),
                new JsonObject
                {
                    ["opencli"] = "0.1-draft",
                    ["commands"] = new JsonArray(),
                });

            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, summaryOutputPath: null, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var state = ParseJsonObject(Path.Combine(repositoryRoot, "state", "packages", "unsafe.tool", "1.0.0.json"));
            Assert.Equal("retryable-failure", state["currentStatus"]?.GetValue<string>());
            Assert.False(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "unsafe.tool", "1.0.0", "metadata.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    [Fact]
    public async Task ApplyUntrustedAsync_MergesMultipleExpectedPlans()
    {
        ToolRuntime.Initialize();

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
                    ["generatedAtUtc"] = "2026-03-27T00:00:00Z",
                    ["packageType"] = "DotnetTool",
                    ["packageCount"] = 2,
                    ["source"] = new JsonObject
                    {
                        ["serviceIndexUrl"] = "https://api.nuget.org/v3/index.json",
                        ["sortOrder"] = "totalDownloads-desc",
                    },
                    ["packages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Alpha.Tool",
                            ["latestVersion"] = "1.0.0",
                            ["totalDownloads"] = 300,
                            ["projectUrl"] = "https://alpha.example",
                        },
                        new JsonObject
                        {
                            ["packageId"] = "Beta.Tool",
                            ["latestVersion"] = "2.0.0",
                            ["totalDownloads"] = 200,
                            ["projectUrl"] = "https://beta.example",
                        },
                    },
                });

            var downloadRoot = Path.Combine(repositoryRoot, "downloads");
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan-a", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-a",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Alpha.Tool",
                            ["version"] = "1.0.0",
                            ["attempt"] = 1,
                            ["totalDownloads"] = 300,
                        },
                    },
                });
            RepositoryPathResolver.WriteJsonFile(
                Path.Combine(downloadRoot, "plan-b", "expected.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["batchId"] = "batch-b",
                    ["targetBranch"] = "main",
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["packageId"] = "Beta.Tool",
                            ["version"] = "2.0.0",
                            ["attempt"] = 1,
                            ["totalDownloads"] = 200,
                        },
                    },
                });

            WriteSuccessAnalysis(downloadRoot, "Alpha.Tool", "1.0.0", "alpha", 300);
            WriteSuccessAnalysis(downloadRoot, "Beta.Tool", "2.0.0", "beta", 200);

            var summaryPath = Path.Combine(repositoryRoot, "promotion-summary.json");
            var service = new PromotionApplyCommandService();
            var exitCode = await service.ApplyUntrustedAsync(downloadRoot, summaryPath, json: true, CancellationToken.None);

            Assert.Equal(0, exitCode);

            var summary = ParseJsonObject(summaryPath);
            Assert.Equal("aggregate-2-plans", summary["batchId"]?.GetValue<string>());
            Assert.Equal(2, summary["expectedCount"]?.GetValue<int>());
            Assert.Equal(2, summary["successCount"]?.GetValue<int>());

            Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "alpha.tool", "1.0.0", "metadata.json")));
            Assert.True(File.Exists(Path.Combine(repositoryRoot, "index", "packages", "beta.tool", "2.0.0", "metadata.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("INSPECTRA_DISCOVERY_REPO_ROOT", previousRepositoryRoot);
        }
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

    private static JsonObject FindPackage(JsonObject manifest, string packageId)
        => manifest["packages"]?.AsArray().OfType<JsonObject>()
               .Single(package => string.Equals(package["packageId"]?.GetValue<string>(), packageId, StringComparison.Ordinal))
           ?? throw new InvalidOperationException($"Package '{packageId}' was not found.");

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

    private static void WriteSuccessAnalysis(string downloadRoot, string packageId, string version, string command, long totalDownloads)
    {
        var artifactDirectory = Path.Combine(downloadRoot, $"analysis-{packageId.ToLowerInvariant()}");
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(artifactDirectory, "result.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = packageId,
                ["version"] = version,
                ["batchId"] = "batch",
                ["attempt"] = 1,
                ["trusted"] = false,
                ["source"] = "analyze-untrusted-batch",
                ["cliFramework"] = "System.CommandLine",
                ["analyzedAt"] = "2026-03-27T01:00:00Z",
                ["disposition"] = "success",
                ["packageUrl"] = $"https://www.nuget.org/packages/{packageId}/{version}",
                ["packageContentUrl"] = $"https://nuget.test/{packageId.ToLowerInvariant()}.{version}.nupkg",
                ["registrationLeafUrl"] = $"https://nuget.test/registration/{packageId.ToLowerInvariant()}/{version}.json",
                ["catalogEntryUrl"] = $"https://nuget.test/catalog/{packageId.ToLowerInvariant()}.{version}.json",
                ["projectUrl"] = $"https://{packageId.ToLowerInvariant()}.example",
                ["publishedAt"] = "2026-03-27T00:30:00Z",
                ["totalDownloads"] = totalDownloads,
                ["command"] = command,
                ["steps"] = new JsonObject
                {
                    ["install"] = new JsonObject
                    {
                        ["status"] = "ok",
                    },
                    ["opencli"] = null,
                    ["xmldoc"] = null,
                },
                ["timings"] = new JsonObject
                {
                    ["totalMs"] = 100,
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliArtifact"] = "opencli.json",
                    ["xmldocArtifact"] = null,
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(artifactDirectory, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = command,
                    ["version"] = "1.0",
                },
                ["commands"] = new JsonArray(),
            });
    }
}
