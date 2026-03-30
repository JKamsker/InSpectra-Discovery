using System.Text.Json.Nodes;
using Xunit;

public sealed class ToolCommandInstallationSupportTests
{
    [Fact]
    public async Task InstallToolAsync_Returns_Command_Context_When_Install_Succeeds()
    {
        using var tempDirectory = new TemporaryDirectory();
        var runtime = new FakeToolCommandRuntime(commandNameToCreate: "demo");
        var result = CreateResultSkeleton();

        var installedTool = await ToolCommandInstallationSupport.InstallToolAsync(
            runtime,
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(installedTool);
        Assert.Equal(Path.Combine(tempDirectory.Path, "tool", "demo"), installedTool.CommandPath);
        Assert.Equal("ok", result["steps"]?["install"]?["status"]?.GetValue<string>());
        Assert.Equal(12, result["timings"]?["installMs"]?.GetValue<int>());
        Assert.True(Directory.Exists(Path.Combine(tempDirectory.Path, "home")));
    }

    [Fact]
    public async Task InstallToolAsync_Applies_Install_Failure_When_Process_Fails()
    {
        using var tempDirectory = new TemporaryDirectory();
        var runtime = new FakeToolCommandRuntime(
            installResult: new ToolCommandRuntime.ProcessResult(
                Status: "failed",
                TimedOut: false,
                ExitCode: 1,
                DurationMs: 42,
                Stdout: string.Empty,
                Stderr: "install exploded"));
        var result = CreateResultSkeleton();

        var installedTool = await ToolCommandInstallationSupport.InstallToolAsync(
            runtime,
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Null(installedTool);
        Assert.Equal("install", result["phase"]?.GetValue<string>());
        Assert.Equal("install-failed", result["classification"]?.GetValue<string>());
        Assert.Equal("install exploded", result["failureMessage"]?.GetValue<string>());
    }

    [Fact]
    public async Task InstallToolAsync_Applies_Command_Missing_Failure_When_Command_File_Is_Not_Present()
    {
        using var tempDirectory = new TemporaryDirectory();
        var runtime = new FakeToolCommandRuntime();
        var result = CreateResultSkeleton();

        var installedTool = await ToolCommandInstallationSupport.InstallToolAsync(
            runtime,
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Null(installedTool);
        Assert.Equal("install", result["phase"]?.GetValue<string>());
        Assert.Equal("installed-command-missing", result["classification"]?.GetValue<string>());
    }

    private static JsonObject CreateResultSkeleton()
        => new()
        {
            ["steps"] = new JsonObject(),
            ["timings"] = new JsonObject(),
            ["artifacts"] = new JsonObject(),
            ["phase"] = null,
            ["classification"] = null,
            ["failureMessage"] = null,
            ["disposition"] = null,
        };

    private sealed class FakeToolCommandRuntime(
        ToolCommandRuntime.ProcessResult? installResult = null,
        string? commandNameToCreate = null) : ToolCommandRuntime
    {
        private readonly ToolCommandRuntime.ProcessResult _installResult = installResult ?? new ToolCommandRuntime.ProcessResult(
            Status: "ok",
            TimedOut: false,
            ExitCode: 0,
            DurationMs: 12,
            Stdout: string.Empty,
            Stderr: string.Empty);
        private readonly string? _commandNameToCreate = commandNameToCreate;

        public override Task<ToolCommandRuntime.ProcessResult> InvokeProcessCaptureAsync(
            string filePath,
            IReadOnlyList<string> argumentList,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment,
            int timeoutSeconds,
            string? sandboxRoot,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_commandNameToCreate))
            {
                var installDirectory = argumentList[^1];
                Directory.CreateDirectory(installDirectory);
                File.WriteAllText(Path.Combine(installDirectory, _commandNameToCreate), string.Empty);
            }

            return Task.FromResult(_installResult);
        }
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
