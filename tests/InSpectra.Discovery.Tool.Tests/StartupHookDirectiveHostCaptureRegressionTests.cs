namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;
using Xunit;

public sealed class StartupHookDirectiveHostCaptureRegressionTests
{
    [Theory]
    [InlineData("weikio-cli", "2024.1.0-preview.37")]
    [InlineData("weikio", "2022.1.2-preview.65")]
    public void Regenerator_Normalizes_Directive_Host_Captures_And_Matches_Frozen_Fixture(string packageId, string version)
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = InitializeRepository(tempDirectory.Path);
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", packageId, version);
        var openCliPath = Path.Combine(versionRoot, "opencli.json");

        RepositoryPathResolver.WriteJsonFile(Path.Combine(versionRoot, "metadata.json"), CreateMetadata(packageId, version));
        RepositoryPathResolver.WriteJsonFile(openCliPath, CreateDirectiveHostCapture(version));

        var regenerator = new StartupHookOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot, new ArtifactRegenerationScope(packageId, version), rebuildIndexes: false);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        HookOpenCliSnapshotSupport.AssertMatchesFixture(packageId, version, ParseJsonObject(openCliPath));
    }

    [Fact]
    public void Repository_StartupHook_OpenCli_Artifacts_Do_Not_Expose_TopLevel_Directives()
    {
        var repositoryRoot = RepositoryPathResolver.ResolveRepositoryRoot();
        var leakedPackages = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "index", "packages"), "metadata.json", SearchOption.AllDirectories)
            .Select(metadataPath => StoredOpenCliArtifactCandidateFactory.TryCreateCandidate(repositoryRoot, metadataPath, "startup-hook"))
            .OfType<StoredOpenCliArtifactCandidate>()
            .Where(candidate => TopLevelCommandsContainDirective(candidate.OpenCliPath))
            .Select(candidate => candidate.DisplayName)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(leakedPackages);
    }

    private static JsonObject CreateMetadata(string packageId, string version)
        => new()
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["analysisMode"] = "hook",
            ["opencliSource"] = "startup-hook",
            ["steps"] = new JsonObject
            {
                ["opencli"] = new JsonObject
                {
                    ["status"] = "ok",
                    ["classification"] = "startup-hook",
                    ["artifactSource"] = "startup-hook",
                },
            },
            ["artifacts"] = new JsonObject
            {
                ["opencliPath"] = $"index/packages/{packageId}/{version}/opencli.json",
                ["opencliSource"] = "startup-hook",
            },
        };

    private static JsonObject CreateDirectiveHostCapture(string version)
        => new()
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = "Weik.io CLI",
                ["version"] = version,
                ["description"] = "CLI for the Weik.io integration platform",
            },
            ["x-inspectra"] = new JsonObject
            {
                ["artifactSource"] = "startup-hook",
                ["cliParsedTitle"] = "weikio",
            },
            ["commands"] = new JsonArray
            {
                new JsonObject { ["name"] = "#i" },
                new JsonObject { ["name"] = "#!who", ["description"] = "Display the names of the current top-level variables." },
                new JsonObject
                {
                    ["name"] = "#!weikio",
                    ["description"] = "Hello Weik.io",
                    ["aliases"] = new JsonArray { "#!w" },
                    ["commands"] = new JsonArray
                    {
                        new JsonObject { ["name"] = "connector", ["description"] = "Integration connector management" },
                        new JsonObject
                        {
                            ["name"] = "integration",
                            ["description"] = "Integration management",
                            ["commands"] = new JsonArray
                            {
                                new JsonObject { ["name"] = "run", ["description"] = "Run integration locally" },
                            },
                        },
                        new JsonObject
                        {
                            ["name"] = "version",
                            ["description"] = "Show the version number",
                            ["aliases"] = new JsonArray { "--version" },
                        },
                    },
                },
            },
        };

    private static bool TopLevelCommandsContainDirective(string openCliPath)
        => ParseJsonObject(openCliPath)["commands"]?.AsArray().OfType<JsonObject>()
            .Any(command => command["name"]?.GetValue<string>()?.StartsWith("#", StringComparison.Ordinal) is true)
           is true;

    private static string InitializeRepository(string root)
    {
        RepositoryPathResolver.WriteTextFile(Path.Combine(root, "InSpectra.Discovery.sln"), string.Empty);
        return root;
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON file '{path}' is empty.");

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"inspectra-tests-{Guid.NewGuid():N}");
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
