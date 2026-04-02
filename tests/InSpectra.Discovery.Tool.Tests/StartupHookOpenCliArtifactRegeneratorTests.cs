namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;
using Xunit;

public sealed class StartupHookOpenCliArtifactRegeneratorTests
{
    [Fact]
    public void Regenerator_Repairs_Stored_StartupHook_OpenCli_And_Backfills_Hook_Metadata()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = InitializeRepository(tempDirectory.Path);
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "hook.tool", "1.0.0");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Hook.Tool",
                ["version"] = "1.0.0",
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
                    ["opencliPath"] = "index/packages/hook.tool/1.0.0/opencli.json",
                    ["opencliSource"] = "startup-hook",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "hook-tool",
                    ["version"] = "1.0.0",
                },
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "--help",
                        ["description"] = "Show help.",
                    },
                },
            });

        var regenerator = new StartupHookOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);

        var openCli = ParseJsonObject(openCliPath);
        Assert.Equal("startup-hook", openCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("hook", metadata["analysisMode"]?.GetValue<string>());
        Assert.Equal("hook", metadata["analysisSelection"]?["selectedMode"]?.GetValue<string>());
        Assert.Equal("hook", metadata["analysisSelection"]?["preferredMode"]?.GetValue<string>());
        Assert.Equal("startup-hook", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
        Assert.Equal("startup-hook", metadata["introspection"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Rejects_Invalid_StartupHook_Host_Capture_Without_Aborting_The_Run()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = InitializeRepository(tempDirectory.Path);
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "host.tool", "1.0.0");

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Host.Tool",
                ["version"] = "1.0.0",
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
                    ["opencliPath"] = "index/packages/host.tool/1.0.0/opencli.json",
                    ["opencliSource"] = "startup-hook",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "opencli.json"),
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "dotnet",
                    ["version"] = "1.0.0",
                },
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "startup-hook",
                },
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "add",
                        ["description"] = "Add a package.",
                    },
                    new JsonObject
                    {
                        ["name"] = "build",
                        ["description"] = "Build a project.",
                    },
                    new JsonObject
                    {
                        ["name"] = "clean",
                        ["description"] = "Clean build outputs.",
                    },
                    new JsonObject
                    {
                        ["name"] = "restore",
                        ["description"] = "Restore dependencies.",
                    },
                    new JsonObject
                    {
                        ["name"] = "test",
                        ["description"] = "Run tests.",
                    },
                    new JsonObject
                    {
                        ["name"] = "tool",
                        ["description"] = "Install a tool.",
                    },
                },
            });

        var regenerator = new StartupHookOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Null(metadata["artifacts"]?["opencliPath"]);
        Assert.Null(metadata["artifacts"]?["opencliSource"]);
        Assert.Equal("invalid-opencli-artifact", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Drops_NonPublishable_Separator_Options_From_Stored_StartupHook_OpenCli()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = InitializeRepository(tempDirectory.Path);
        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "hook.tool", "1.0.0");
        var openCliPath = Path.Combine(versionRoot, "opencli.json");

        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = "Hook.Tool",
                ["version"] = "1.0.0",
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
                    ["opencliPath"] = "index/packages/hook.tool/1.0.0/opencli.json",
                    ["opencliSource"] = "startup-hook",
                },
            });
        RepositoryPathResolver.WriteJsonFile(
            openCliPath,
            new JsonObject
            {
                ["opencli"] = "0.1-draft",
                ["info"] = new JsonObject
                {
                    ["title"] = "hook-tool",
                    ["version"] = "1.0.0",
                },
                ["x-inspectra"] = new JsonObject
                {
                    ["artifactSource"] = "startup-hook",
                },
                ["options"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼",
                        ["description"] = "separator row",
                    },
                    new JsonObject
                    {
                        ["name"] = "--help",
                        ["description"] = "Show help.",
                    },
                },
            });

        var regenerator = new StartupHookOpenCliArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.Equal(0, result.FailedCount);

        var openCli = ParseJsonObject(openCliPath);
        var options = openCli["options"]!.AsArray();
        Assert.DoesNotContain(options, option => option?["name"]?.GetValue<string>() == "⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼⎼");
        Assert.Contains(options, option => option?["name"]?.GetValue<string>() == "--help");
    }

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
