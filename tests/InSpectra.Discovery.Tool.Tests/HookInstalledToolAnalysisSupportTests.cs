namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Hook;
using InSpectra.Discovery.Tool.Analysis.NonSpectre;
using InSpectra.Discovery.Tool.Infrastructure.Commands;

using System.Text.Json;
using System.Text.Json.Nodes;

using Xunit;

public sealed class HookInstalledToolAnalysisSupportTests
{
    [Fact]
    public async Task AnalyzeAsync_Applies_Missing_Capture_Failure_With_Process_Diagnostics()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var capturePath = Path.Combine(tempDirectory.Path, "inspectra-capture.json");
        var runtime = new FakeHookCommandRuntime("demo", invocation =>
        {
            Assert.Equal(Path.Combine(tempDirectory.Path, "tool", "demo.cmd"), invocation.FilePath);
            Assert.Single(invocation.ArgumentList);
            Assert.Equal("--help", invocation.ArgumentList[0]);
            Assert.Equal(hookDllPath, invocation.Environment["DOTNET_STARTUP_HOOKS"]);
            Assert.Equal(capturePath, invocation.Environment["INSPECTRA_CAPTURE_PATH"]);
            Assert.Equal(
                "System.CommandLine",
                invocation.Environment[HookInstalledToolAnalysisSupport.ExpectedCliFrameworkEnvironmentVariableName]);

            return new CommandRuntime.ProcessResult(
                Status: "failed",
                TimedOut: false,
                ExitCode: -532462766,
                DurationMs: 27,
                Stdout: string.Empty,
                Stderr: "Unhandled exception. System.NullReferenceException: boom");
        });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult();

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("retryable-failure", result["disposition"]?.GetValue<string>());
        Assert.Equal("hook-capture", result["phase"]?.GetValue<string>());
        Assert.Equal("hook-no-capture-file", result["classification"]?.GetValue<string>());

        var failureMessage = result["failureMessage"]?.GetValue<string>();
        Assert.NotNull(failureMessage);
        Assert.Contains("Exit code: -532462766.", failureMessage);
        Assert.Contains("stderr: Unhandled exception. System.NullReferenceException: boom", failureMessage);
    }

    [Fact]
    public async Task AnalyzeAsync_Writes_OpenCli_Artifact_When_Hook_Capture_Succeeds()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var runtime = new FakeHookCommandRuntime("demo", invocation =>
        {
            var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
            File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
            {
                CaptureVersion = 1,
                Status = "ok",
                CliFramework = "System.CommandLine",
                FrameworkVersion = "2.0.0",
                SystemCommandLineVersion = "2.0.0",
                PatchTarget = "Parse-postfix",
                Root = new HookCapturedCommand
                {
                    Name = "demo",
                    Description = "Demo CLI",
                    Options =
                    [
                        new HookCapturedOption
                        {
                            Name = "--verbose",
                            Description = "Verbose output.",
                            ValueType = "Boolean",
                            Aliases = ["-v"],
                        },
                    ],
                    Subcommands =
                    [
                        new HookCapturedCommand
                        {
                            Name = "serve",
                            Description = "Start the server.",
                        },
                    ],
                },
            }));

            return new CommandRuntime.ProcessResult(
                Status: "ok",
                TimedOut: false,
                ExitCode: 0,
                DurationMs: 15,
                Stdout: string.Empty,
                Stderr: string.Empty);
        });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult();

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("complete", result["phase"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["opencliSource"]?.GetValue<string>());
        Assert.Equal("opencli.json", result["artifacts"]?["opencliArtifact"]?.GetValue<string>());

        var openCliPath = Path.Combine(tempDirectory.Path, "opencli.json");
        Assert.True(File.Exists(openCliPath));

        var openCli = JsonNode.Parse(File.ReadAllText(openCliPath))!.AsObject();
        Assert.Equal("demo", openCli["info"]?["title"]?.GetValue<string>());
        Assert.Equal("1.2.3", openCli["info"]?["version"]?.GetValue<string>());
        Assert.Equal("startup-hook", openCli["x-inspectra"]?["artifactSource"]?.GetValue<string>());
        Assert.Equal("System.CommandLine", openCli["x-inspectra"]?["hookCapture"]?["cliFramework"]?.GetValue<string>());
        Assert.Equal("2.0.0", openCli["x-inspectra"]?["hookCapture"]?["frameworkVersion"]?.GetValue<string>());
        Assert.Contains(openCli["options"]!.AsArray(), option => option?["name"]?.GetValue<string>() == "--verbose");
        Assert.Contains(openCli["commands"]!.AsArray(), command => command?["name"]?.GetValue<string>() == "serve");
    }

    [Fact]
    public async Task AnalyzeAsync_Uses_Dotnet_Runner_From_Installed_Tool_Settings_When_Available()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var runtime = new FakeHookCommandRuntime(
            "demo",
            installDirectoryInitializer: installDirectory =>
            {
                var toolDirectory = Path.Combine(installDirectory, ".store", "demo.tool", "1.2.3", "demo.tool", "1.2.3", "tools", "net6.0", "any");
                Directory.CreateDirectory(toolDirectory);
                File.WriteAllText(Path.Combine(toolDirectory, "Demo.Tool.dll"), string.Empty);
                File.WriteAllText(Path.Combine(toolDirectory, "Demo.Tool.runtimeconfig.json"), "{}");
                File.WriteAllText(
                    Path.Combine(toolDirectory, "DotnetToolSettings.xml"),
                    """
                    <?xml version="1.0" encoding="utf-8"?>
                    <DotNetCliTool Version="1">
                      <Commands>
                        <Command Name="demo" EntryPoint="Demo.Tool.dll" Runner="dotnet" />
                      </Commands>
                    </DotNetCliTool>
                    """);
            },
            hookHandler: invocation =>
            {
                Assert.Equal("dotnet", Path.GetFileNameWithoutExtension(invocation.FilePath));
                Assert.Equal(2, invocation.ArgumentList.Length);
                Assert.EndsWith(Path.Combine("tools", "net6.0", "any", "Demo.Tool.dll"), invocation.ArgumentList[0], StringComparison.OrdinalIgnoreCase);
                Assert.Equal("--help", invocation.ArgumentList[1]);

                var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
                File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
                {
                    CaptureVersion = 1,
                    Status = "ok",
                    CliFramework = "System.CommandLine",
                    FrameworkVersion = "2.0.0",
                    SystemCommandLineVersion = "2.0.0",
                    PatchTarget = "Parse-postfix",
                    Root = new HookCapturedCommand
                    {
                        Name = "demo",
                        Description = "Demo CLI",
                    },
                }));

                return new CommandRuntime.ProcessResult(
                    Status: "ok",
                    TimedOut: false,
                    ExitCode: 0,
                    DurationMs: 15,
                    Stdout: string.Empty,
                    Stderr: string.Empty);
            });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult();

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["opencliSource"]?.GetValue<string>());
        Assert.Equal("opencli.json", result["artifacts"]?["opencliArtifact"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnalyzeAsync_Falls_Back_To_CommandPath_When_Dotnet_Runner_Missing_RuntimeConfig()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var runtime = new FakeHookCommandRuntime(
            "demo",
            installDirectoryInitializer: installDirectory =>
            {
                var toolDirectory = Path.Combine(installDirectory, ".store", "demo.tool", "1.2.3", "demo.tool", "1.2.3", "tools", "custom", "any");
                Directory.CreateDirectory(toolDirectory);
                File.WriteAllText(Path.Combine(toolDirectory, "Demo.Tool.dll"), string.Empty);
                File.WriteAllText(
                    Path.Combine(toolDirectory, "DotnetToolSettings.xml"),
                    """
                    <?xml version="1.0" encoding="utf-8"?>
                    <DotNetCliTool Version="1">
                      <Commands>
                        <Command Name="demo" EntryPoint="Demo.Tool.dll" Runner="dotnet" />
                      </Commands>
                    </DotNetCliTool>
                    """);
            },
            hookHandler: invocation =>
            {
                Assert.Equal(Path.Combine(tempDirectory.Path, "tool", "demo.cmd"), invocation.FilePath);
                Assert.Single(invocation.ArgumentList);
                Assert.Equal("--help", invocation.ArgumentList[0]);

                var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
                File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
                {
                    CaptureVersion = 1,
                    Status = "ok",
                    CliFramework = "System.CommandLine",
                    FrameworkVersion = "2.0.0",
                    SystemCommandLineVersion = "2.0.0",
                    PatchTarget = "Parse-postfix",
                    Root = new HookCapturedCommand
                    {
                        Name = "demo",
                        Description = "Demo CLI",
                    },
                }));

                return new CommandRuntime.ProcessResult(
                    Status: "ok",
                    TimedOut: false,
                    ExitCode: 0,
                    DurationMs: 15,
                    Stdout: string.Empty,
                    Stderr: string.Empty);
            });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult();

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnalyzeAsync_Synthesizes_RuntimeConfig_For_Dotnet_Runner_When_Missing()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var runtime = new FakeHookCommandRuntime(
            "demo",
            installDirectoryInitializer: installDirectory =>
            {
                var toolDirectory = Path.Combine(installDirectory, ".store", "demo.tool", "1.2.3", "demo.tool", "1.2.3", "tools", "net8.0", "any");
                Directory.CreateDirectory(toolDirectory);
                File.WriteAllText(Path.Combine(toolDirectory, "Demo.Tool.dll"), string.Empty);
                File.WriteAllText(
                    Path.Combine(toolDirectory, "DotnetToolSettings.xml"),
                    """
                    <?xml version="1.0" encoding="utf-8"?>
                    <DotNetCliTool Version="1">
                      <Commands>
                        <Command Name="demo" EntryPoint="Demo.Tool.dll" Runner="dotnet" />
                      </Commands>
                    </DotNetCliTool>
                    """);
            },
            hookHandler: invocation =>
            {
                Assert.Equal("dotnet", Path.GetFileNameWithoutExtension(invocation.FilePath));
                Assert.Equal(2, invocation.ArgumentList.Length);
                var runtimeConfigPath = Path.ChangeExtension(invocation.ArgumentList[0], ".runtimeconfig.json");
                Assert.True(File.Exists(runtimeConfigPath));

                var runtimeConfig = JsonNode.Parse(File.ReadAllText(runtimeConfigPath))!.AsObject();
                Assert.Equal("net8.0", runtimeConfig["runtimeOptions"]?["tfm"]?.GetValue<string>());
                Assert.Equal("Microsoft.NETCore.App", runtimeConfig["runtimeOptions"]?["framework"]?["name"]?.GetValue<string>());
                Assert.Equal("8.0.0", runtimeConfig["runtimeOptions"]?["framework"]?["version"]?.GetValue<string>());

                var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
                File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
                {
                    CaptureVersion = 1,
                    Status = "ok",
                    CliFramework = "System.CommandLine",
                    FrameworkVersion = "2.0.0",
                    SystemCommandLineVersion = "2.0.0",
                    PatchTarget = "Parse-postfix",
                    Root = new HookCapturedCommand
                    {
                        Name = "demo",
                        Description = "Demo CLI",
                    },
                }));

                return new CommandRuntime.ProcessResult(
                    Status: "ok",
                    TimedOut: false,
                    ExitCode: 0,
                    DurationMs: 15,
                    Stdout: string.Empty,
                    Stderr: string.Empty);
            });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult();

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["opencliSource"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnalyzeAsync_Retries_With_Short_Help_Switch_When_Long_Help_Is_Rejected()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var invocationCount = 0;
        var runtime = new FakeHookCommandRuntime("demo", invocation =>
        {
            invocationCount++;
            if (invocationCount == 1)
            {
                Assert.Equal("--help", invocation.ArgumentList[^1]);
                return new CommandRuntime.ProcessResult(
                    Status: "failed",
                    TimedOut: false,
                    ExitCode: 1,
                    DurationMs: 15,
                    Stdout: string.Empty,
                    Stderr: "Unrecognized option '--help'");
            }

            Assert.Equal(2, invocationCount);
            Assert.Equal("-h", invocation.ArgumentList[^1]);

            var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
            File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
            {
                CaptureVersion = 1,
                Status = "ok",
                CliFramework = "Microsoft.Extensions.CommandLineUtils",
                FrameworkVersion = "2.2.0.0",
                PatchTarget = "Execute-postfix",
                Root = new HookCapturedCommand
                {
                    Name = "demo",
                    Description = "Demo CLI",
                },
            }));

            return new CommandRuntime.ProcessResult(
                Status: "ok",
                TimedOut: false,
                ExitCode: 0,
                DurationMs: 15,
                Stdout: string.Empty,
                Stderr: string.Empty);
        });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult("Microsoft.Extensions.CommandLineUtils");

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(2, invocationCount);
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnalyzeAsync_Retries_With_Short_Help_Switch_When_Capture_Reports_Rejected_Long_Help()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var invocationCount = 0;
        var runtime = new FakeHookCommandRuntime("demo", invocation =>
        {
            invocationCount++;
            var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
            if (invocationCount == 1)
            {
                Assert.Equal("--help", invocation.ArgumentList[^1]);
                File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
                {
                    CaptureVersion = 1,
                    Status = "target-unhandled-exception",
                    Error = "Microsoft.Extensions.CommandLineUtils.CommandParsingException: Unrecognized option '--help'",
                    CliFramework = "Microsoft.Extensions.CommandLineUtils",
                    FrameworkVersion = "2.2.0.0",
                }));

                return new CommandRuntime.ProcessResult(
                    Status: "failed",
                    TimedOut: false,
                    ExitCode: 1,
                    DurationMs: 15,
                    Stdout: string.Empty,
                    Stderr: string.Empty);
            }

            Assert.Equal(2, invocationCount);
            Assert.Equal("-h", invocation.ArgumentList[^1]);
            File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
            {
                CaptureVersion = 1,
                Status = "ok",
                CliFramework = "Microsoft.Extensions.CommandLineUtils",
                FrameworkVersion = "2.2.0.0",
                PatchTarget = "Execute-postfix",
                Root = new HookCapturedCommand
                {
                    Name = "demo",
                    Description = "Demo CLI",
                },
            }));

            return new CommandRuntime.ProcessResult(
                Status: "ok",
                TimedOut: false,
                ExitCode: 0,
                DurationMs: 15,
                Stdout: string.Empty,
                Stderr: string.Empty);
        });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult("Microsoft.Extensions.CommandLineUtils");

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(2, invocationCount);
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnalyzeAsync_Retries_With_DoubleDashShortHelp_When_Other_Help_Switches_Are_Rejected()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var invocationCount = 0;
        var runtime = new FakeHookCommandRuntime("demo", invocation =>
        {
            invocationCount++;
            if (invocationCount == 1)
            {
                Assert.Equal("--help", invocation.ArgumentList[^1]);
            }
            else if (invocationCount == 2)
            {
                Assert.Equal("-h", invocation.ArgumentList[^1]);
            }
            else if (invocationCount == 3)
            {
                Assert.Equal("-?", invocation.ArgumentList[^1]);
            }
            else
            {
                Assert.Equal(4, invocationCount);
                Assert.Equal("--h", invocation.ArgumentList[^1]);

                var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
                File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
                {
                    CaptureVersion = 1,
                    Status = "ok",
                    CliFramework = "McMaster.Extensions.CommandLineUtils",
                    FrameworkVersion = "4.0.0.0",
                    PatchTarget = "Execute-postfix",
                    Root = new HookCapturedCommand
                    {
                        Name = "demo",
                        Description = "Demo CLI",
                    },
                }));

                return new CommandRuntime.ProcessResult(
                    Status: "ok",
                    TimedOut: false,
                    ExitCode: 0,
                    DurationMs: 15,
                    Stdout: string.Empty,
                    Stderr: string.Empty);
            }

            return new CommandRuntime.ProcessResult(
                Status: "failed",
                TimedOut: false,
                ExitCode: 0,
                DurationMs: 15,
                Stdout: string.Empty,
                Stderr: "Need to insert a value for the option");
        });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult("McMaster.Extensions.CommandLineUtils");

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(4, invocationCount);
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["opencliSource"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnalyzeAsync_Retries_With_Invariant_Globalization_When_Icu_Is_Missing()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var invocationCount = 0;
        var runtime = new FakeHookCommandRuntime("demo", invocation =>
        {
            invocationCount++;
            if (invocationCount == 1)
            {
                Assert.False(invocation.Environment.ContainsKey(HookInstalledToolAnalysisSupport.GlobalizationInvariantEnvironmentVariableName));
                return new CommandRuntime.ProcessResult(
                    Status: "failed",
                    TimedOut: false,
                    ExitCode: 134,
                    DurationMs: 15,
                    Stdout: string.Empty,
                    Stderr: "Couldn't find a valid ICU package installed on the system. Set the configuration flag System.Globalization.Invariant to true if you want to run with no globalization support.");
            }

            Assert.Equal(2, invocationCount);
            Assert.Equal(
                "1",
                invocation.Environment[HookInstalledToolAnalysisSupport.GlobalizationInvariantEnvironmentVariableName]);

            var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
            File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
            {
                CaptureVersion = 1,
                Status = "ok",
                CliFramework = "Microsoft.Extensions.CommandLineUtils",
                FrameworkVersion = "2.2.0.0",
                PatchTarget = "Execute-postfix",
                Root = new HookCapturedCommand
                {
                    Name = "demo",
                    Description = "Demo CLI",
                },
            }));

            return new CommandRuntime.ProcessResult(
                Status: "ok",
                TimedOut: false,
                ExitCode: 0,
                DurationMs: 15,
                Stdout: string.Empty,
                Stderr: string.Empty);
        });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult("Microsoft.Extensions.CommandLineUtils");

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(2, invocationCount);
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnalyzeAsync_Retries_With_DotnetRollForward_When_Shared_Runtime_Is_Missing()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var invocationCount = 0;
        var runtime = new FakeHookCommandRuntime("demo", invocation =>
        {
            invocationCount++;
            if (invocationCount == 1)
            {
                Assert.False(invocation.Environment.ContainsKey(HookInstalledToolAnalysisSupport.DotnetRollForwardEnvironmentVariableName));
                return new CommandRuntime.ProcessResult(
                    Status: "failed",
                    TimedOut: false,
                    ExitCode: -2147450730,
                    DurationMs: 15,
                    Stdout: string.Empty,
                    Stderr: """
                            You must install or update .NET to run this application.

                            Framework: 'Microsoft.NETCore.App', version '6.0.0' (x64)
                            The following frameworks were found:
                              8.0.15 at [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
                            """);
            }

            Assert.Equal(2, invocationCount);
            Assert.Equal(
                HookInstalledToolAnalysisSupport.DotnetRollForwardMajorValue,
                invocation.Environment[HookInstalledToolAnalysisSupport.DotnetRollForwardEnvironmentVariableName]);

            var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
            File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
            {
                CaptureVersion = 1,
                Status = "ok",
                CliFramework = "Microsoft.Extensions.CommandLineUtils",
                FrameworkVersion = "2.2.0.0",
                PatchTarget = "Execute-postfix",
                Root = new HookCapturedCommand
                {
                    Name = "demo",
                    Description = "Demo CLI",
                },
            }));

            return new CommandRuntime.ProcessResult(
                Status: "ok",
                TimedOut: false,
                ExitCode: 0,
                DurationMs: 15,
                Stdout: string.Empty,
                Stderr: string.Empty);
        });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult("Microsoft.Extensions.CommandLineUtils");

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(2, invocationCount);
        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["opencliSource"]?.GetValue<string>());
    }

    [Fact]
    public async Task AnalyzeAsync_Passes_Resolved_Hook_Framework_To_Startup_Hook()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var hookDllPath = CreateHookPlaceholder(tempDirectory.Path);
        var runtime = new FakeHookCommandRuntime("demo", invocation =>
        {
            Assert.Equal(
                "McMaster.Extensions.CommandLineUtils",
                invocation.Environment[HookInstalledToolAnalysisSupport.ExpectedCliFrameworkEnvironmentVariableName]);

            var capturePath = invocation.Environment["INSPECTRA_CAPTURE_PATH"];
            File.WriteAllText(capturePath, JsonSerializer.Serialize(new HookCaptureResult
            {
                CaptureVersion = 1,
                Status = "ok",
                CliFramework = "McMaster.Extensions.CommandLineUtils",
                FrameworkVersion = "4.0.0.0",
                PatchTarget = "Execute-postfix",
                Root = new HookCapturedCommand
                {
                    Name = "demo",
                    Description = "Demo CLI",
                },
            }));

            return new CommandRuntime.ProcessResult(
                Status: "ok",
                TimedOut: false,
                ExitCode: 0,
                DurationMs: 15,
                Stdout: string.Empty,
                Stderr: string.Empty);
        });
        var support = new HookInstalledToolAnalysisSupport(runtime, () => hookDllPath);
        var result = CreateInitialResult("McMaster.Extensions.CommandLineUtils + Argu");

        await support.AnalyzeAsync(
            result,
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            outputDirectory: tempDirectory.Path,
            tempRoot: tempDirectory.Path,
            installTimeoutSeconds: 30,
            commandTimeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("success", result["disposition"]?.GetValue<string>());
        Assert.Equal("startup-hook", result["classification"]?.GetValue<string>());
    }

    private static JsonObject CreateInitialResult(string cliFramework = "System.CommandLine")
        => NonSpectreResultSupport.CreateInitialResult(
            packageId: "Demo.Tool",
            version: "1.2.3",
            commandName: "demo",
            batchId: "batch-001",
            attempt: 1,
            source: "unit-test",
            cliFramework: cliFramework,
            analysisMode: "hook",
            analyzedAt: DateTimeOffset.Parse("2026-03-31T00:00:00Z"));

    private static string CreateHookPlaceholder(string tempRoot)
    {
        var hookPath = Path.Combine(tempRoot, "hooks", "InSpectra.Discovery.StartupHook.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(hookPath)!);
        File.WriteAllText(hookPath, string.Empty);
        return hookPath;
    }

    private sealed class FakeHookCommandRuntime(
        string commandName,
        Func<HookInvocation, CommandRuntime.ProcessResult> hookHandler,
        Action<string>? installDirectoryInitializer = null) : CommandRuntime
    {
        public override Task<ProcessResult> InvokeProcessCaptureAsync(
            string filePath,
            IReadOnlyList<string> argumentList,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment,
            int timeoutSeconds,
            string? sandboxRoot,
            CancellationToken cancellationToken)
        {
            if (string.Equals(filePath, "dotnet", StringComparison.OrdinalIgnoreCase)
                && argumentList.Count >= 2
                && string.Equals(argumentList[0], "tool", StringComparison.OrdinalIgnoreCase)
                && string.Equals(argumentList[1], "install", StringComparison.OrdinalIgnoreCase))
            {
                var installDirectory = argumentList[^1];
                Directory.CreateDirectory(installDirectory);
                File.WriteAllText(Path.Combine(installDirectory, commandName + ".cmd"), "@echo off");
                installDirectoryInitializer?.Invoke(installDirectory);

                return Task.FromResult(new CommandRuntime.ProcessResult(
                    Status: "ok",
                    TimedOut: false,
                    ExitCode: 0,
                    DurationMs: 12,
                    Stdout: string.Empty,
                    Stderr: string.Empty));
            }

            var invocation = new HookInvocation(
                filePath,
                argumentList.ToArray(),
                new Dictionary<string, string>(environment, StringComparer.OrdinalIgnoreCase));
            return Task.FromResult(hookHandler(invocation));
        }
    }

    private sealed record HookInvocation(
        string FilePath,
        string[] ArgumentList,
        IReadOnlyDictionary<string, string> Environment);
}
