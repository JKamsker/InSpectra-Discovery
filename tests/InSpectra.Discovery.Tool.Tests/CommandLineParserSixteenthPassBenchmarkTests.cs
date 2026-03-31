namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Artifacts;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;
using Xunit;

public sealed class CommandLineParserSixteenthPassBenchmarkTests
{
    [Fact]
    public void Regenerator_Does_Not_Infer_Positional_Args_From_Unrecognized_Markdown_Sections()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.clp-markdown-sections", "1.0.0");
        WriteMetadata(versionRoot, "sample.clp-markdown-sections", "1.0.0", "tranalyze", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            tranalyze
            Copyright (c) 2023 Ken Domino
            # tranalyze

            ## Summary

            Analyze a grammar

            ## Description

            Reads an Antlr4 grammar in the form of parse tree data from stdin,
            searches for problems in the grammar, and outputs the results to stdout.

            ## Usage

                tranalyze

            ## Details

            `tranalyze` performs a multi-pass search of a grammar in the
            form of a parse result, looking for problems in the grammar.

            ## Example

                trparse Test.g4 | tranalyze

              -f, --file           Read parse tree data from file instead of stdin.
              --fmt                Output formatted parsing results set.
              -s, --start-rules    Start rule names.
              -v, --verbose
              --version
              --help               Display this help screen.
              --version            Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));

        Assert.Null(openCli["arguments"]);
    }

    [Fact]
    public void Regenerator_Does_Not_Infer_Positional_Args_From_Usage_Examples_In_Unrecognized_Sections()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.clp-usage-examples", "1.0.0");
        WriteMetadata(versionRoot, "sample.clp-usage-examples", "1.0.0", "nanoff", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            nanoff 2.5.131+6bd4d0daab
            Copyright (c) .NET Foundation and Contributors
            USAGE:
            - Update ESP32 WROVER Kit device with latest available firmware:
              nanoff --target ESP_WROVER_KIT --update
            - Update specific STM32 device with custom firmware (local bin file):
              nanoff --image "<location of file>.bin" --target ESP_WROVER_KIT
            - Update specific Silabs device (Giant Gecko EVK) with latest available
            firmware:
              nanoff --target SL_STK3701A --update
            - List all available STM32 targets:
              nanoff --listtargets --platform stm32

              --listdfu         (Default: false) List connected DFU devices.

              --target          Target name. This is the target name used in
                                the GitHub and Cloudsmith repositories.

              --platform        Target platform. Acceptable values are: esp32,
                                stm32, cc13x2, efm32.

              --update          (Default: false) Update the device firmware
                                using the other specified options.

              --help            Display this help screen.

              --version         Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));

        Assert.Null(openCli["arguments"]);
    }

    [Fact]
    public void Regenerator_Does_Not_Infer_Positional_Args_From_Example_Invocations()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.clp-example-invocations", "1.0.0");
        WriteMetadata(versionRoot, "sample.clp-example-invocations", "1.0.0", "netenv", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            netenv 1.0.1
            Copyright (C) 2026 Joshua Searles
            USAGE:
            Run using local .env file:
              dotenv
            Run using specified file:
              dotenv --file=.env.local

              -f, --file       (Default: .env) Location of the .env file.

              --help           Display this help screen.

              --version        Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));

        // "dotenv" should NOT be inferred as a positional argument
        Assert.Null(openCli["arguments"]);
    }

    [Fact]
    public void Regenerator_Keeps_Legitimate_Usage_Positionals_But_Rejects_Bogus_Ones()
    {
        Runtime.Initialize();

        using var tempDirectory = new TemporaryDirectory();
        var repositoryRoot = tempDirectory.Path;
        RepositoryPathResolver.WriteTextFile(Path.Combine(repositoryRoot, "InSpectra.Discovery.sln"), string.Empty);

        var versionRoot = Path.Combine(repositoryRoot, "index", "packages", "sample.clp-usage-positionals", "1.0.0");
        WriteMetadata(versionRoot, "sample.clp-usage-positionals", "1.0.0", "trcombine", rejectedHelpArtifact: true);
        WriteCrawl(versionRoot,
            """
            trcombine
            Copyright (c) 2023 Ken Domino
            # trcombine

            ## Summary

            Combine a split Antlr4 grammar

            ## Description

            Combine two grammars into one.
            One grammar must be a lexer grammar, the other a parser grammar,
            order is irrelevant. The output is parse tree data.

            ## Usage

                trcombine <grammar1> <grammar2>

            ## Details

            `trcombine` combines grammars that are known as "split grammars"
            (separate Antlr4 lexer and parser grammars)
            into one grammar, known as a "combined grammar". This refactoring is
            useful if a simplified grammar grammar is wanted.

            The split refactoring performs several operations:

            * Combine the two files together, parser grammar first, then lexer grammar.
            * Remove the `grammarDecl` for the lexer rules, and change the `grammarDecl`
            for the parser rules to be a combined grammar declaration.
            * Remove the `optionsSpec` for the lexer section.
            * Remove any occurrence of "tokenVocab" from the `optionsSpec` of the parser
            section.
            * If the `optionsSpec` is empty, it is removed.

            ## Example

                trcombine ExpressionLexer.g4 ExpressionParser.g4 | trprint > Expression.g4

            ## Current version

            0.23.43 Fixes to trgen templates.

            ## License

            The MIT License


              -f, --file       Read parse tree data from file instead of stdin.
              --fmt            Output formatted parsing results set.
              -v, --verbose
              --version
              --help           Display this help screen.
              --version        Display version information.
            """);

        var regenerator = new CrawlArtifactRegenerator();
        var result = regenerator.RegenerateRepository(repositoryRoot);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.RewrittenCount);

        var openCli = ParseJsonObject(Path.Combine(versionRoot, "opencli.json"));

        var arguments = openCli["arguments"]?.AsArray();
        Assert.NotNull(arguments);
        Assert.Equal(2, arguments.Count);
        Assert.Equal("GRAMMAR1", arguments[0]!["name"]!.GetValue<string>());
        Assert.Equal("GRAMMAR2", arguments[1]!["name"]!.GetValue<string>());
        // SECTION should NOT appear as a third argument
    }

    private static JsonObject? FindOption(JsonArray options, string name)
        => options
            .OfType<JsonObject>()
            .FirstOrDefault(option => string.Equals(option["name"]?.GetValue<string>(), name, StringComparison.Ordinal));

    private static void WriteMetadata(string versionRoot, string packageId, string version, string command, bool rejectedHelpArtifact)
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
                ["status"] = rejectedHelpArtifact ? "partial" : "ok",
                ["analysisMode"] = "help",
                ["steps"] = new JsonObject
                {
                    ["opencli"] = new JsonObject
                    {
                        ["artifactSource"] = rejectedHelpArtifact ? null : "crawled-from-help",
                        ["classification"] = rejectedHelpArtifact ? "invalid-opencli-artifact" : null,
                    },
                },
                ["artifacts"] = new JsonObject
                {
                    ["opencliSource"] = rejectedHelpArtifact ? null : "crawled-from-help",
                },
            });
    }

    private static void WriteCrawl(string versionRoot, string payload)
    {
        RepositoryPathResolver.WriteJsonFile(
            Path.Combine(versionRoot, "crawl.json"),
            new JsonObject
            {
                ["commands"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["command"] = null,
                        ["payload"] = payload,
                    },
                },
            });
    }

    private static JsonObject ParseJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path))?.AsObject()
           ?? throw new InvalidOperationException($"JSON object expected at '{path}'.");

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


