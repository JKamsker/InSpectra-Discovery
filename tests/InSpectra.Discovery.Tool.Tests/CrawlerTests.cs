namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Crawling;

using InSpectra.Discovery.Tool.Analysis.Execution;
using InSpectra.Discovery.Tool.Infrastructure.Commands;

using Xunit;

public sealed class CrawlerTests
{
    [Fact]
    public void BuildHelpInvocations_Includes_Switches_Keyword_Forms_And_BareInvocation()
    {
        var invocations = Crawler.BuildHelpInvocations(["config", "set"])
            .Select(arguments => string.Join(' ', arguments))
            .ToArray();

        Assert.Equal(
            new[]
            {
                "config set --help",
                "config set -h",
                "config set -?",
                "config set --h",
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
        var runtime = new FakeCommandRuntime(arguments =>
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
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "demo",
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
        var runtime = new FakeCommandRuntime(arguments =>
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
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "demo",
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
    public async Task CrawlAsync_Continues_To_DoubleDashShortHelp_When_RequiredValue_Prompts_Are_Returned()
    {
        var runtime = new FakeCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            return key switch
            {
                "--help" or "-h" or "-?" => Result(
                    stdout:
                    """
                    Need to insert a value for the option
                    """),
                "--h" => Result(
                    stdout:
                    """
                    sqlite-tool

                    Options:
                      -db  Sqlite Database Path
                    """),
                _ => throw new InvalidOperationException($"Unexpected invocation: '{key}'."),
            };
        });
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "demo",
            "demo",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(new[] { "--help", "-h", "-?", "--h" }, runtime.Invocations.Select(args => string.Join(' ', args)).ToArray());
        Assert.True(result.Documents.ContainsKey(string.Empty));
        Assert.Equal("--h", result.CaptureSummaries[string.Empty].HelpInvocation);
    }

    [Fact]
    public async Task CrawlAsync_Normalizes_RootQualified_Subcommands_Using_Root_Command_Name()
    {
        var runtime = new FakeCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            return key switch
            {
                "--help" => Result(
                    stdout:
                    """
                    CYCODT - AI-powered CLI Test Framework

                    USAGE: cycodt <command> [...]

                    COMMANDS

                      cycodt list [...]       Lists CLI YAML tests
                    """),
                "list --help" => Result(
                    stdout:
                    """
                    CYCODT LIST

                      The cycodt list command lists CLI YAML tests.

                    USAGE: cycodt list [...]

                      --file FILE  Read tests from file.
                    """),
                _ => throw new InvalidOperationException($"Unexpected invocation: '{key}'."),
            };
        });
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            @"C:\tools\cycodt.exe",
            "cycodt",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(["", "list"], result.Documents.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        Assert.Equal("list --help", result.CaptureSummaries["list"].HelpInvocation);
        Assert.DoesNotContain(runtime.Invocations.Select(args => string.Join(' ', args)), invocation => invocation.StartsWith("cycodt list", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CrawlAsync_Stops_Probing_A_Subcommand_After_Terminal_NonHelp_Output()
    {
        var runtime = new FakeCommandRuntime(arguments =>
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
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "demo",
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
    public async Task CrawlAsync_Infers_Nested_Subcommands_From_Usage_Lines_When_Command_Sections_Are_Missing()
    {
        var runtime = new FakeCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            if (key is "expect --help" or "expect -h" or "expect -?" or "expect --h" or "expect /help" or "expect /?" or "expect")
            {
                return Result(
                    stdout: "Invalid argument: expect",
                    exitCode: 1);
            }

            return key switch
            {
                "--help" => Result(
                    stdout:
                    """
                    demo 1.0.0

                    Usage: demo <command>

                    Commands:
                      expect  Manage expectations.
                    """),
                "help expect" => Result(
                    stdout:
                    """
                    demo expect

                    Usage: demo expect check [options]
                       OR: demo expect format [options]
                    """),
                "expect check --help" => Result(
                    stdout:
                    """
                    demo expect check

                    Options:
                      --instructions  Instructions for the validator.
                    """),
                "expect format --help" => Result(
                    stdout:
                    """
                    demo expect format

                    Options:
                      --save-output  Save the formatted output.
                    """),
                _ => throw new InvalidOperationException($"Unexpected invocation: '{key}'."),
            };
        });
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "demo",
            "demo",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(
            ["", "expect", "expect check", "expect format"],
            result.Documents.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        Assert.Equal("help expect", result.CaptureSummaries["expect"].HelpInvocation);
        Assert.Equal("expect check --help", result.CaptureSummaries["expect check"].HelpInvocation);
        Assert.Equal("expect format --help", result.CaptureSummaries["expect format"].HelpInvocation);
    }

    [Fact]
    public async Task CrawlAsync_Infers_Root_Subcommands_From_Multiline_Usage_Inventories()
    {
        var runtime = new FakeCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            return key switch
            {
                "--help" => Result(
                    stdout:
                    """
                    mcpdebugger - AI-controlled cooperative debugger via MCP

                    Usage:
                      mcpdebugger serve [--port <port>]   Start the HTTP debug server (default port: 5200)
                      mcpdebugger mcp [--port <port>]     Start the MCP server (talks to debug server)
                      mcpdebugger --help                  Show this help message
                    """),
                "serve --help" => Result(
                    stdout:
                    """
                    mcpdebugger serve

                    Options:
                      --port  Port for the HTTP debug server.
                    """),
                "mcp --help" => Result(
                    stdout:
                    """
                    mcpdebugger mcp

                    Options:
                      --port  Port for the MCP bridge.
                    """),
                _ => throw new InvalidOperationException($"Unexpected invocation: '{key}'."),
            };
        });
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "mcpdebugger",
            "mcpdebugger",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(["", "mcp", "serve"], result.Documents.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(["--help", "mcp --help", "serve --help"], runtime.Invocations.Select(args => string.Join(' ', args)).OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        Assert.Equal("serve --help", result.CaptureSummaries["serve"].HelpInvocation);
        Assert.Equal("mcp --help", result.CaptureSummaries["mcp"].HelpInvocation);
    }

    [Fact]
    public async Task CrawlAsync_Prefers_Single_Stream_Help_Payload_Over_Invocation_Echo_Combination()
    {
        var runtime = new FakeCommandRuntime(arguments =>
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
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "Spriggit.Yaml.Skyrim",
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
        var runtime = new FakeCommandRuntime(arguments =>
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
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "Spriggit.Yaml.Skyrim",
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

    [Fact]
    public async Task CrawlAsync_DoesNot_Recurse_Into_NonRoot_Dispatcher_Echoes()
    {
        var runtime = new FakeCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            if (string.Equals(key, "--help", StringComparison.Ordinal))
            {
                return Result(
                    stdout:
                    """
                    mortis - local PR remediation agent

                    Commands:
                      mortis start <folder> [--full]  Scaffold a Mortis host folder
                      mortis run --config <file>      Run Mortis using a config json
                      mortis doctor                   Check prerequisites
                      mortis version                  Print versions
                    """);
            }

            if (arguments.Any(argument => argument is "start" or "run" or "doctor" or "version"))
            {
                return Result(
                    stdout:
                    """
                    mortis - local PR remediation agent

                    Commands:
                      mortis start <folder> [--full]  Scaffold a Mortis host folder
                      mortis run --config <file>      Run Mortis using a config json
                      mortis doctor                   Check prerequisites
                      mortis version                  Print versions
                    """);
            }

            throw new InvalidOperationException($"Unexpected invocation: '{key}'.");
        });
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "mortis",
            "mortis",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(
            ["", "doctor", "run", "start"],
            result.CaptureSummaries.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        Assert.Equal([""], result.Documents.Keys);
        Assert.DoesNotContain(runtime.Invocations.Select(args => string.Join(' ', args)), invocation => invocation.Contains("doctor run", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(runtime.Invocations.Select(args => string.Join(' ', args)), invocation => invocation.Contains("start run", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CrawlAsync_DoesNot_Treat_Status_Table_Echoes_As_Nested_Subcommands()
    {
        var runtime = new FakeCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            if (string.Equals(key, "--help", StringComparison.Ordinal))
            {
                return Result(
                    stdout:
                    """
                    netagents - package manager for .agents directories

                    Usage: netagents [--user] <command> [options]

                    Commands:
                      list    Show installed skills
                    """);
            }

            if (arguments.Contains("list", StringComparer.OrdinalIgnoreCase))
            {
                return Result(
                    stdout:
                    """
                    Skills:
                      x netagents  getsentry/dotagents  not installed
                    """);
            }

            throw new InvalidOperationException($"Unexpected invocation: '{key}'.");
        });
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "netagents",
            "netagents",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(["", "list"], result.CaptureSummaries.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        Assert.Equal([""], result.Documents.Keys);
        Assert.DoesNotContain(runtime.Invocations.Select(args => string.Join(' ', args)), invocation => invocation.Contains("x netagents", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CrawlAsync_DoesNot_Treat_Singular_Example_Sections_As_Subcommands()
    {
        var runtime = new FakeCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            if (string.Equals(key, "--help", StringComparison.Ordinal))
            {
                return Result(
                    stdout:
                    """
                    SqlDatabase Command Line Tools (v6.0.2)
                    Usage: DbCmd <command> [options]

                    Commands:
                      GenerateEntities   | ge     Generate a file with entities.
                      Merge              | mg     Merge all script files.

                    'GenerateEntities' options:
                      --ConnectionString | -cs    Required. Connection string to the database server.

                    Example:
                      DbCmd GenerateEntities -cs="Server=localhost;Database=Scott;"
                    """);
            }

            if (arguments.Contains("GenerateEntities", StringComparer.OrdinalIgnoreCase)
                || arguments.Contains("Merge", StringComparer.OrdinalIgnoreCase))
            {
                return Result(
                    stdout:
                    """
                    Please, specify a command to execute: GenerateEntities, ...
                    Write DbCmd --help for more information.
                    """,
                    exitCode: 1);
            }

            throw new InvalidOperationException($"Unexpected invocation: '{key}'.");
        });
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "DbCmd",
            "DbCmd",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.DoesNotContain(result.CaptureSummaries.Keys, key => string.Equals(key, "Example", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(runtime.Invocations.Select(args => string.Join(' ', args)), invocation => invocation.StartsWith("Example", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CrawlAsync_Treats_Scaffolded_Output_As_Terminal_NonHelp_And_DoesNot_Recurse()
    {
        var runtime = new FakeCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            if (string.Equals(key, "--help", StringComparison.Ordinal))
            {
                return Result(
                    stdout:
                    """
                    EthisysCore CLI v0.0.0+build

                    Usage:
                      cc generate feature <name>

                    Commands:
                      generate feature  Scaffold a new plugin project

                    Flags:
                      --version, -v  Print the CLI version
                    """,
                    exitCode: 1);
            }

            if (string.Equals(key, "generate feature --help", StringComparison.Ordinal))
            {
                return Result(
                    stdout:
                    """
                    Plugin 'Help' scaffolded at: /tmp/inspectra-help-demo/--help

                      Solution:   --help.slnx
                      Structure:  Default (single project)

                    Next steps:
                      1. cd --help
                      2. dotnet build
                    """);
            }

            throw new InvalidOperationException($"Unexpected invocation: '{key}'.");
        });
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "cc",
            "cc",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal(["", "generate feature"], result.CaptureSummaries.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        Assert.True(result.CaptureSummaries["generate feature"].TerminalNonHelp);
        Assert.Equal(["--help", "generate feature --help"], runtime.Invocations.Select(args => string.Join(' ', args)));
        Assert.DoesNotContain(result.CaptureSummaries.Keys, key => key.Contains("Structure", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.CaptureSummaries.Keys, key => key.Contains("Flags", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CrawlAsync_Rejects_NonRoot_Dispatcher_Echoes_Even_When_They_Include_Global_Flags()
    {
        var runtime = new FakeCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            if (string.Equals(key, "--help", StringComparison.Ordinal))
            {
                return Result(
                    stdout:
                    """
                    EthisysCore CLI v0.0.0+build

                    Usage:
                      cc generate feature <name>
                      cc generate from-schema <schema.json>

                    Commands:
                      generate feature      Scaffold a new plugin project
                      generate from-schema  Generate from schema

                    Flags:
                      --version, -v  Print the CLI version
                    """,
                    exitCode: 1);
            }

            if (arguments.Contains("generate", StringComparer.OrdinalIgnoreCase)
                && arguments.Contains("from-schema", StringComparer.OrdinalIgnoreCase))
            {
                return Result(
                    stdout:
                    """
                    EthisysCore CLI v0.0.0+build

                    Usage:
                      cc generate feature <name>
                      cc generate from-schema <schema.json>

                    Commands:
                      generate feature      Scaffold a new plugin project
                      generate from-schema  Generate from schema

                    Flags:
                      --version, -v  Print the CLI version
                    """,
                    exitCode: 1);
            }

            if (string.Equals(key, "generate feature --help", StringComparison.Ordinal))
            {
                return Result(
                    stdout:
                    """
                    Plugin 'Help' scaffolded at: /tmp/inspectra-help-demo/--help

                    Next steps:
                      1. cd --help
                    """);
            }

            throw new InvalidOperationException($"Unexpected invocation: '{key}'.");
        });
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "cc",
            "cc",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.Equal([""], result.Documents.Keys);
        Assert.Equal(["", "generate feature", "generate from-schema"], result.CaptureSummaries.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.CaptureSummaries.Keys, key => key.StartsWith("generate from-schema ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CrawlAsync_Fails_When_A_Command_Exceeds_The_Child_Command_Budget()
    {
        var commandRows = Enumerable.Range(1, HelpCrawlGuardrailSupport.MaxChildCommandsPerDocument + 1)
            .Select(index => $"  cmd{index:D2}  Command {index}.")
            .ToArray();
        var runtime = new FakeCommandRuntime(arguments =>
        {
            var key = string.Join(' ', arguments);
            if (string.Equals(key, "--help", StringComparison.Ordinal))
            {
                return Result(
                    stdout:
                    $$"""
                    demo 1.0.0

                    Usage: demo <command>

                    Commands:
                    {{string.Join(Environment.NewLine, commandRows)}}
                    """);
            }

            throw new InvalidOperationException($"Unexpected invocation: '{key}'.");
        });
        var crawler = new Crawler(runtime);

        var result = await crawler.CrawlAsync(
            "demo",
            "demo",
            workingDirectory: Environment.CurrentDirectory,
            environment: new Dictionary<string, string>(),
            timeoutSeconds: 30,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result.GuardrailFailureMessage);
        Assert.Contains(HelpCrawlGuardrailSupport.MaxChildCommandsPerDocument.ToString(), result.GuardrailFailureMessage);
        Assert.Equal([""], result.Documents.Keys);
        Assert.Equal(["--help"], runtime.Invocations.Select(args => string.Join(' ', args)));
    }

    private static CommandRuntime.ProcessResult Result(string? stdout = null, string? stderr = null, int exitCode = 0, bool timedOut = false)
        => new(
            Status: timedOut ? "timed-out" : exitCode == 0 ? "ok" : "failed",
            TimedOut: timedOut,
            ExitCode: timedOut ? null : exitCode,
            DurationMs: 1,
            Stdout: stdout ?? string.Empty,
            Stderr: stderr ?? string.Empty);

    private sealed class FakeCommandRuntime : CommandRuntime
    {
        private readonly Func<IReadOnlyList<string>, ProcessResult> _handler;

        public FakeCommandRuntime(Func<IReadOnlyList<string>, ProcessResult> handler)
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
