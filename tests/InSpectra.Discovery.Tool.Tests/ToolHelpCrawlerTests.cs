using Xunit;

public sealed class ToolHelpCrawlerTests
{
    [Fact]
    public void BuildHelpInvocations_Includes_Switches_Keyword_Forms_And_BareInvocation()
    {
        var invocations = ToolHelpCrawler.BuildHelpInvocations(["config", "set"])
            .Select(arguments => string.Join(' ', arguments))
            .ToArray();

        Assert.Equal(
            new[]
            {
                "config set --help",
                "config set -h",
                "config set -?",
                "config set /help",
                "config set /?",
                "help config set",
                "config help set",
                "config set help",
                "config set",
            },
            invocations);
    }

    [Fact]
    public async Task CrawlAsync_Continues_After_Rejected_Help_Switch_Until_A_Valid_Probe_Succeeds()
    {
        var runtime = new FakeToolCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            if (string.Equals(key, "convert-from-plugin --help", StringComparison.Ordinal)
                || string.Equals(key, "help convert-from-plugin", StringComparison.Ordinal)
                || string.Equals(key, "help convert-from-plugin --help", StringComparison.Ordinal))
            {
                return Result(
                    stderr:
                    """
                    Spriggit.Yaml.Skyrim 0.41.0

                      -i, --InputPath    Required. Path to the plugin.
                    """,
                    exitCode: 1);
            }

            return key switch
            {
                "--help" => Result(
                    stdout:
                    """
                    --help is an unknown parameter
                    Usage: demo [options]
                    """,
                    exitCode: 1),
                "-h" => Result(
                    stdout:
                    """
                    demo 1.0.0

                    Usage: demo [options]

                    Options:
                      --verbose  Verbose output.
                    """),
                _ => throw new InvalidOperationException($"Unexpected invocation: '{key}'."),
            };
        });
        var crawler = new ToolHelpCrawler(runtime);

        var result = await crawler.CrawlAsync(
            "demo",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(new[] { "--help", "-h" }, runtime.Invocations.Select(args => string.Join(' ', args)).ToArray());
        Assert.True(result.Documents.ContainsKey(string.Empty));
        Assert.Equal("-h", result.CaptureSummaries[string.Empty].HelpInvocation);
    }

    [Fact]
    public async Task CrawlAsync_Continues_After_CommandLineParser_BadVerb_Help_Probe()
    {
        var runtime = new FakeToolCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            return key switch
            {
                "--help" => Result(
                    stdout:
                    """
                    ERROR(S):
                      Verb '--help' is not recognized.

                      --help    Display this help screen.
                    """,
                    exitCode: 1),
                "-h" => Result(
                    stdout:
                    """
                    ERROR(S):
                      Verb '-h' is not recognized.

                      --help    Display this help screen.
                    """,
                    exitCode: 1),
                "-?" => Result(
                    stdout:
                    """
                    demo 1.0.0

                      --verbose    Verbose output.
                      --help       Display this help screen.
                    """),
                _ => throw new InvalidOperationException($"Unexpected invocation: '{key}'."),
            };
        });
        var crawler = new ToolHelpCrawler(runtime);

        var result = await crawler.CrawlAsync(
            "demo",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(new[] { "--help", "-h", "-?" }, runtime.Invocations.Select(args => string.Join(' ', args)).ToArray());
        Assert.True(result.Documents.ContainsKey(string.Empty));
        Assert.Equal("-?", result.CaptureSummaries[string.Empty].HelpInvocation);
    }

    [Fact]
    public async Task CrawlAsync_Stops_Probing_A_Subcommand_After_Terminal_NonHelp_Output()
    {
        var runtime = new FakeToolCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            return key switch
            {
                "--help" => Result(
                    stdout:
                    """
                    demo 1.0.0

                    Usage: demo [command]

                    Commands:
                      serve  Start the server.
                    """),
                "serve --help" => Result(
                    stderr:
                    """
                    Unhandled exception. System.InvalidOperationException: boom
                       at Program.Main(String[] args)
                    """,
                    exitCode: 1),
                _ => throw new InvalidOperationException($"Unexpected invocation: '{key}'."),
            };
        });
        var crawler = new ToolHelpCrawler(runtime);

        var result = await crawler.CrawlAsync(
            "demo",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(new[] { "--help", "serve --help" }, runtime.Invocations.Select(args => string.Join(' ', args)).ToArray());
        Assert.True(result.Documents.ContainsKey(string.Empty));
        Assert.False(result.Documents.ContainsKey("serve"));
        Assert.True(result.CaptureSummaries["serve"].TerminalNonHelp);
        Assert.Equal("serve --help", result.CaptureSummaries["serve"].HelpInvocation);
    }

    [Fact]
    public async Task CrawlAsync_Prefers_Single_Stream_Help_Payload_Over_Invocation_Echo_Combination()
    {
        var runtime = new FakeToolCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            if (string.Equals(key, "convert-from-plugin --help", StringComparison.Ordinal)
                || string.Equals(key, "help convert-from-plugin", StringComparison.Ordinal)
                || string.Equals(key, "help convert-from-plugin --help", StringComparison.Ordinal))
            {
                return Result(
                    stderr:
                    """
                    Spriggit.Yaml.Skyrim 0.41.0

                      -i, --InputPath    Required. Path to the plugin.
                    """,
                    exitCode: 1);
            }

            return key switch
            {
                "--help" => Result(
                    stdout:
                    """
                    Spriggit version 0.41.0
                    --help
                    """,
                    stderr:
                    """
                    Spriggit.Yaml.Skyrim 0.41.0
                    2024

                      serialize, convert-from-plugin    Converts a plugin to text.

                      help                              Display more information on a specific command.
                    """,
                    exitCode: 1),
                "help --help" => Result(
                    stdout:
                    """
                    Spriggit version 0.41.0
                    help --help
                    """,
                    stderr:
                    """
                    Spriggit.Yaml.Skyrim 0.41.0
                    2024

                      serialize, convert-from-plugin    Converts a plugin to text.

                      help                              Display more information on a specific command.
                    """,
                    exitCode: 1),
                _ => throw new InvalidOperationException($"Unexpected invocation: '{key}'."),
            };
        });
        var crawler = new ToolHelpCrawler(runtime);

        var result = await crawler.CrawlAsync(
            "Spriggit.Yaml.Skyrim",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal("Spriggit.Yaml.Skyrim", result.Documents[string.Empty].Title);
        Assert.Contains(result.Documents[string.Empty].Commands, command => string.Equals(command.Key, "convert-from-plugin", StringComparison.Ordinal));
        Assert.Equal("convert-from-plugin --help", result.CaptureSummaries["convert-from-plugin"].HelpInvocation);
    }

    [Fact]
    public async Task CrawlAsync_DoesNot_Recurse_Into_Builtin_Auxiliary_Inventory_Echoes()
    {
        var runtime = new FakeToolCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            return key switch
            {
                "--help" => Result(
                    stderr:
                    """
                    Spriggit.Yaml.Skyrim 0.41.0
                    2024

                      serialize, convert-from-plugin    Converts a plugin to text.

                      help                              Display more information on a specific command.

                      version                           Display version information.
                    """,
                    exitCode: 1),
                "convert-from-plugin --help" => Result(
                    stderr:
                    """
                    Spriggit.Yaml.Skyrim 0.41.0

                      -i, --InputPath    Required. Path to the plugin.
                    """,
                    exitCode: 1),
                "help --help" => Result(
                    stderr:
                    """
                    Spriggit.Yaml.Skyrim 0.41.0
                    2024

                      serialize, convert-from-plugin    Converts a plugin to text.

                      help                              Display more information on a specific command.

                      version                           Display version information.
                    """,
                    exitCode: 1),
                "version --help" => Result(
                    stderr: "Spriggit.Yaml.Skyrim 0.41.0",
                    exitCode: 1),
                _ => throw new InvalidOperationException($"Unexpected invocation: '{key}'."),
            };
        });
        var crawler = new ToolHelpCrawler(runtime);

        var result = await crawler.CrawlAsync(
            "Spriggit.Yaml.Skyrim",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(["", "convert-from-plugin"], result.Documents.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.CaptureSummaries.Keys, key => string.Equals(key, "help", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.CaptureSummaries.Keys, key => string.Equals(key, "version", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.CaptureSummaries.Keys, key => key.StartsWith("help ", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.CaptureSummaries.Keys, key => key.StartsWith("version ", StringComparison.OrdinalIgnoreCase));
    }

    private static ToolCommandRuntime.ProcessResult Result(string? stdout = null, string? stderr = null, int exitCode = 0, bool timedOut = false)
        => new(
            Status: timedOut ? "timed-out" : exitCode == 0 ? "ok" : "failed",
            TimedOut: timedOut,
            ExitCode: timedOut ? null : exitCode,
            DurationMs: 1,
            Stdout: stdout ?? string.Empty,
            Stderr: stderr ?? string.Empty);

    private sealed class FakeToolCommandRuntime : ToolCommandRuntime
    {
        private readonly Func<IReadOnlyList<string>, ProcessResult> _handler;

        public FakeToolCommandRuntime(Func<IReadOnlyList<string>, ProcessResult> handler)
        {
            _handler = handler;
        }

        public List<string[]> Invocations { get; } = [];

        public override Task<ProcessResult> InvokeProcessCaptureAsync(
            string filePath,
            IReadOnlyList<string> argumentList,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment,
            int timeoutSeconds,
            string? sandboxRoot,
            CancellationToken cancellationToken)
        {
            var invocation = argumentList.ToArray();
            Invocations.Add(invocation);
            return Task.FromResult(_handler(invocation));
        }
    }
}
