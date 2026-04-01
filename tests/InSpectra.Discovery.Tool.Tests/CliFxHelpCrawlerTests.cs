namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.CliFx.Crawling;
using InSpectra.Discovery.Tool.Infrastructure.Commands;

using Xunit;

public sealed class CliFxHelpCrawlerTests
{
    [Fact]
    public async Task CrawlAsync_Retries_With_DotnetRollForward_When_Shared_Runtime_Is_Missing()
    {
        var runtime = new FakeCommandRuntime();
        var crawler = new CliFxHelpCrawler(runtime);

        var result = await crawler.CrawlAsync(
            "demo",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Documents.ContainsKey(string.Empty));
        Assert.Equal(2, runtime.Invocations.Count);
        Assert.Equal("--help", runtime.Invocations[0].Arguments[0]);
        Assert.Equal("--help", runtime.Invocations[1].Arguments[0]);
        Assert.False(runtime.Invocations[0].Environment.ContainsKey(DotnetRuntimeCompatibilitySupport.DotnetRollForwardEnvironmentVariableName));
        Assert.Equal(
            DotnetRuntimeCompatibilitySupport.DotnetRollForwardMajorValue,
            runtime.Invocations[1].Environment[DotnetRuntimeCompatibilitySupport.DotnetRollForwardEnvironmentVariableName]);
        Assert.Equal("--help", result.CaptureSummaries[string.Empty].HelpSwitch);
    }

    private static CommandRuntime.ProcessResult MissingFrameworkResult()
        => new(
            Status: "failed",
            TimedOut: false,
            ExitCode: -2147450730,
            DurationMs: 1,
            Stdout: string.Empty,
            Stderr:
            """
            You must install or update .NET to run this application.

            Framework: 'Microsoft.NETCore.App', version '9.0.0' (x64)
            The following frameworks were found:
              10.0.5 at [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
            """);

    private static CommandRuntime.ProcessResult HelpResult()
        => new(
            Status: "ok",
            TimedOut: false,
            ExitCode: 0,
            DurationMs: 1,
            Stdout:
            """
            demo

            USAGE
              demo [options]

            OPTIONS
              --verbose  Verbose output.
            """,
            Stderr: string.Empty);

    private sealed class FakeCommandRuntime : CommandRuntime
    {
        public List<InvocationRecord> Invocations { get; } = [];

        public override Task<ProcessResult> InvokeProcessCaptureAsync(
            string filePath,
            IReadOnlyList<string> argumentList,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment,
            int timeoutSeconds,
            string? sandboxRoot,
            CancellationToken cancellationToken)
        {
            var invocation = new InvocationRecord(
                argumentList.ToArray(),
                new Dictionary<string, string>(environment, StringComparer.OrdinalIgnoreCase));
            Invocations.Add(invocation);

            var result = invocation.Environment.ContainsKey(DotnetRuntimeCompatibilitySupport.DotnetRollForwardEnvironmentVariableName)
                ? HelpResult()
                : MissingFrameworkResult();
            return Task.FromResult(result);
        }
    }

    private sealed record InvocationRecord(
        string[] Arguments,
        IReadOnlyDictionary<string, string> Environment);
}
