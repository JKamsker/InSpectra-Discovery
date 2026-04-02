namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Artifacts;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class SuspiciousHelpPayloadRegressionTests
{
    [Fact]
    public void Regenerator_Treats_Aspose_Style_Bare_Short_Long_Rows_As_Options_And_Ignores_Nested_Help_Errors()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "aspose.psd.cli.nlp.editor", "24.6.0");
        WriteMetadata(versionRoot, "Aspose.PSD.CLI.NLP.Editor", "24.6.0", "aspose-psd-nlp-editor");
        WriteCrawl(
            versionRoot,
            (null,
                """
                Aspose.PSD.CLI.NLP-Editor for .NET 7 24.6
                Copyright © 2001-2024 Aspose Pty Ltd.

                  v, verbose    If specified the output of app will be verbose
                  s, setup      If specified then in User Folder will be created script for
                                short synonym of tool
                  c, command    Command for NLP Editor in natural Language
                  license       Path to the license.
                  help          Display more information on a specific command.
                  version       Display version information.
                """),
            ("command Input",
                """
                Something went wrong.
                Unknown operation: help

                Aspose.PSD.CLI.NLP-Editor for .NET 7 24.6
                Copyright © 2001-2024 Aspose Pty Ltd.

                  v, verbose    If specified the output of app will be verbose
                  s, setup      If specified then in User Folder will be created script for
                                short synonym of tool
                  c, command    Command for NLP Editor in natural Language
                  license       Path to the license.
                  help          Display more information on a specific command.
                  version       Display version information.
                """));

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var optionNames = GetOptionNames(openCli);

        Assert.Contains("--verbose", optionNames);
        Assert.Contains("--setup", optionNames);
        Assert.Contains("--command", optionNames);
        Assert.False(ContainsCommandNamed(openCli, "command"));
        Assert.False(ContainsCommandNamed(openCli, "setup"));
        Assert.False(ContainsCommandNamed(openCli, "verbose"));
    }

    [Fact]
    public void Regenerator_Drops_Mortis_Style_Root_Dispatcher_Echos_For_NonRoot_Captures()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "mortis.tool", "0.4.3");
        WriteMetadata(versionRoot, "Mortis.Tool", "0.4.3", "mortis");
        WriteCrawl(
            versionRoot,
            (null,
                """
                mortis - local PR remediation agent

                Commands:
                  mortis start <folder> [--full]  Scaffold a Mortis host folder
                                                  --full creates MortisHost.Web + MortisHost.UI
                  mortis run --config <file>      Run Mortis using a config json
                  mortis doctor                   Check prerequisites
                  mortis version                  Print versions
                """),
            ("mortis doctor",
                """
                mortis - local PR remediation agent

                Commands:
                  mortis start <folder> [--full]  Scaffold a Mortis host folder
                                                  --full creates MortisHost.Web + MortisHost.UI
                  mortis run --config <file>      Run Mortis using a config json
                  mortis doctor                   Check prerequisites
                  mortis version                  Print versions
                """));

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.False(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("partial", metadata["status"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Drops_Cycodt_Style_Invalid_Argument_Root_Echos_For_NonRoot_Captures()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "cycodt", "1.0.0-alpha-20260123.2");
        WriteMetadata(versionRoot, "CycoDt", "1.0.0-alpha-20260123.2", "cycodt");
        WriteCrawl(
            versionRoot,
            (null,
                """
                CYCODT - AI-powered CLI Test Framework, Version 1.0.0-alpha-20260123.2
                Copyright(c) 2025, Rob Chambers. All rights reserved.

                USAGE: cycodt <command> [...]

                COMMANDS

                  cycodt list [...]       Lists CLI YAML tests
                  cycodt run [...]        Runs CLI YAML tests
                  cycodt expect [...]     Manage LLM and regex expectations
                """),
            ("cycodt expect",
                """
                CYCODT - AI-powered CLI Test Framework, Version 1.0.0-alpha-20260123.2
                Copyright(c) 2025, Rob Chambers. All rights reserved.

                Invalid argument: cycodt

                USAGE: cycodt <command> [...]

                COMMANDS

                  cycodt list [...]       Lists CLI YAML tests
                  cycodt run [...]        Runs CLI YAML tests
                  cycodt expect [...]     Manage LLM and regex expectations
                """));

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);
        Assert.False(File.Exists(Path.Combine(versionRoot, "opencli.json")));

        var metadata = ParseJsonObject(Path.Combine(versionRoot, "metadata.json"));
        Assert.Equal("partial", metadata["status"]?.GetValue<string>());
        Assert.Equal("invalid-opencli-artifact", metadata["steps"]?["opencli"]?["classification"]?.GetValue<string>());
    }

    [Fact]
    public void Regenerator_Ignores_Netagents_Status_Tables_For_NonRoot_Captures()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "netagents", "0.2.0");
        WriteMetadata(versionRoot, "NetAgents", "0.2.0", "netagents");
        WriteCrawl(
            versionRoot,
            (null,
                """
                netagents - package manager for .agents directories

                Usage: netagents [--user] <command> [options]

                Commands:
                  init        Initialize agents.toml and .agents/skills/
                  install     Install dependencies from agents.toml
                  add         Add a skill dependency
                  remove      Remove a skill dependency
                  sync        Reconcile gitignore, symlinks, verify state
                  list        Show installed skills
                  mcp         Manage MCP server declarations
                  trust       Manage trusted sources
                  doctor      Check project health and fix issues

                Options:
                  --user      Operate on user-scope (~/.agents/) instead of project
                  --help, -h  Show this help message
                  --version   Show version
                """),
            ("list",
                """
                Skills:
                  x netagents  getsentry/dotagents  not installed
                """));

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        var listCommand = FindRootCommand(openCli, "list");

        Assert.NotNull(listCommand);
        Assert.Null(listCommand!["commands"]);
    }

    [Fact]
    public void Regenerator_Preserves_Usage_Arguments_And_Does_Not_Promote_Them_To_Commands()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "fsdgencsharp", "2.38.0");
        WriteMetadata(versionRoot, "FsdGenCSharp", "2.38.0", "fsdgencsharp");
        WriteCrawl(
            versionRoot,
            (null,
                """
                Generates C# for a Facility Service Definition.

                Usage: fsdgencsharp input output [options]

                   input
                      The path to the input file (- for stdin).
                   output
                      The path to the output directory.

                   --nullable
                      Use nullable reference syntax in the generated C#.
                   --dry-run
                      Executes the tool without making changes to the file system.
                """));

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));
        Assert.True((openCli["commands"]?.AsArray().Count ?? 0) == 0);

        var arguments = openCli["arguments"]?.AsArray().OfType<JsonObject>().ToArray() ?? [];
        var argumentNames = arguments.Select(argument => argument["name"]?.GetValue<string>()).ToArray();
        Assert.Collection(
            argumentNames,
            name => Assert.Equal("INPUT", name),
            name => Assert.Equal("OUTPUT", name));

        var optionNames = GetOptionNames(openCli);
        Assert.Contains("--nullable", optionNames);
        Assert.Contains("--dry-run", optionNames);
    }

    private static void WriteMetadata(string versionRoot, string packageId, string version, string command)
    {
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "metadata.json"),
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["packageId"] = packageId,
                ["version"] = version,
                ["command"] = command,
                ["cliFramework"] = "CommandLineParser",
                ["status"] = "partial",
                ["analysisMode"] = "help",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["classification"] = "invalid-opencli-artifact",
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliSource"] = null,
                },
            });
    }

    private static void WriteCrawl(string versionRoot, params (string? Command, string Payload)[] captures)
    {
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "crawl.json"),
            new JsonObject
            {
                ["commands"] = new JsonArray(captures.Select(capture => new JsonObject
                {
                    ["command"] = capture.Command,
                    ["payload"] = capture.Payload,
                }).ToArray()),
            });
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON object expected at '{path}'.");

    private static IReadOnlyList<string> GetOptionNames(JsonObject openCli)
        => openCli["options"]?.AsArray()
            .OfType<JsonObject>()
            .Select(option => option["name"]?.GetValue<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray()
            ?? [];

    private static IReadOnlyList<string> GetRootCommandNames(JsonObject openCli)
        => openCli["commands"]?.AsArray()
            .OfType<JsonObject>()
            .Select(command => command["name"]?.GetValue<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray()
            ?? [];

    private static JsonObject? FindRootCommand(JsonObject openCli, string commandName)
        => openCli["commands"]?.AsArray()
            .OfType<JsonObject>()
            .FirstOrDefault(command => string.Equals(command["name"]?.GetValue<string>(), commandName, StringComparison.Ordinal));

    private static bool ContainsCommandNamed(JsonObject openCli, string commandName)
        => ContainsCommandNamed(openCli["commands"] as JsonArray, commandName);

    private static bool ContainsCommandNamed(JsonArray? commands, string commandName)
    {
        foreach (var command in commands?.OfType<JsonObject>() ?? [])
        {
            if (string.Equals(command["name"]?.GetValue<string>(), commandName, StringComparison.Ordinal))
            {
                return true;
            }

            if (ContainsCommandNamed(command["commands"] as JsonArray, commandName))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
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
