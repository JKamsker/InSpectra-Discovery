namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.Inference.Text;
using InSpectra.Discovery.Tool.Help.Inference.Usage;
using InSpectra.Discovery.Tool.Help.OpenCli;
using InSpectra.Discovery.Tool.Help.Parsing;

using System.Text.Json.Nodes;

using Xunit;

public sealed class HelpNoiseRegressionTests
{
    [Fact]
    public void Ignores_Ascii_Banner_And_Repository_Preamble_Lines()
    {
        var parser = new TextParser();

        var document = parser.Parse(
            """
            (\_/)
              ( >_<)  o     forge for .NET Aspire (MVW)
             <|  | )--|     Issues / PRs welcome at github.com/rudiv/forge
              |___|

            USAGE:
                forge [OPTIONS] [COMMAND]

            COMMANDS:
                fire    Fire up the AppHost for local development (dotnet watch)
            """);

        Assert.Null(document.Title);
        Assert.Null(document.ApplicationDescription);
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "fire", StringComparison.Ordinal));
    }

    [Fact]
    public void Parses_German_Command_Headers_Without_Leaking_Them_Into_Option_Descriptions()
    {
        var parser = new TextParser();

        var document = parser.Parse(
            """
            (\_/)
              ( >_<)  o     forge for .NET Aspire (MVW)
             <|  | )--|     Issues / PRs welcome at github.com/rudiv/forge
              |___|

            VERWENDUNG:
                forge [OPTIONEN] [KOMMANDO]

            OPTIONEN:
                -h, --help       Zeigt Hilfe an
                -v, --version    Zeigt Versionsinformationen an

            KOMMANDOS:
                fire    Fire up the AppHost for local development (dotnet watch)

            ?? Shutting down...
            """);

        Assert.Contains(document.Commands, command => string.Equals(command.Key, "fire", StringComparison.Ordinal));

        var version = Assert.Single(document.Options.Where(option => option.Key.Contains("--version", StringComparison.Ordinal)));
        Assert.DoesNotContain("KOMMANDOS", version.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("Shutting down", version.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Does_Not_Attach_Shutdown_Footers_To_Option_Descriptions()
    {
        var parser = new TextParser();

        var document = parser.Parse(
            """
            (\_/)
              ( >_<)  o     forge for .NET Aspire (MVW)
             <|  | )--|     Issues / PRs welcome at github.com/rudiv/forge
              |___|

            DESCRIPTION:
            Fire up the AppHost for local development (dotnet watch).

            USAGE:
                forge fire [OPTIONS]

            OPTIONS:
                                              DEFAULT
                -h, --help                               Prints help information
                -v, --version                            Prints version information
                    --apphost-build-output
                    --no-hot-reload
                -p, --port                    6969       Set the runtime port for the DCP
                                                         Integration host

            🧽 Shutting down...
            """);

        var port = Assert.Single(document.Options.Where(option => option.Key.Contains("--port", StringComparison.Ordinal)));
        Assert.Contains("Integration host", port.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("Shutting down", port.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Does_Not_Parse_Separator_Rows_As_Options()
    {
        var parser = new TextParser();

        var dotnetCliZip = parser.Parse(
            """
            dotnet cli zip
            ---------------
            """);
        var entityFrameworkRuler = parser.Parse(
            """
            Entity Framework Ruler
            -----------------------
            Rule Generation Usage:
            efruler -g <edmxfilepath> <efCoreProjectBasePath>
            efruler -a <pathContainingRulesAndCsProj>
            """);
        var biak = parser.Parse(
            """
            Enable / Disable .editorconfig rules
            ---
            * dotnet biak setup | Setup command
            --------------------
            """);

        Assert.Empty(dotnetCliZip.Options);
        Assert.Empty(entityFrameworkRuler.Options);
        Assert.Empty(biak.Options);
    }

    [Fact]
    public void Keeps_Box_Table_Continuation_Rows_After_Their_Entry_Row()
    {
        var lines = new[]
        {
            "┌───────────────────────┬───────────────────────────────┐",
            "│ Option                │ Description                   │",
            "├───────────────────────┼───────────────────────────────┤",
            "│ -h, -?, --help        │ Show help and usage           │",
            "│ --log-path <PATH>     │ Enable file logging           │",
            "│                       │ to the specified directory    │",
            "└───────────────────────┴───────────────────────────────┘",
        };

        var normalized = StructuredOptionTableSupport.TryExtractStructuredOptionLines(lines);

        Assert.Equal(
            [
                "-h, -?, --help  Show help and usage",
                "--log-path <PATH>  Enable file logging",
                "to the specified directory",
            ],
            normalized);
    }

    [Fact]
    public void Keeps_Parsed_Box_Table_Continuations_On_The_Current_Option()
    {
        var items = ItemParser.ParseItems(
            [
                "-h, -?, --help  Show help and usage",
                "--log-path <PATH>  Enable file logging",
                "to the specified directory",
            ],
            ItemKind.Option);

        var logPath = Assert.Single(items.Where(option => string.Equals(option.Key, "--log-path <PATH>", StringComparison.Ordinal)));
        Assert.Contains("specified directory", logPath.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Does_Not_Reclassify_Box_Table_Option_Rows_As_Arguments_During_Splitting()
    {
        ItemParser.SplitArgumentSectionLines(
            [
                "-h, -?, --help  Show help and usage",
                "--log-path <PATH>  Enable file logging",
                "to the specified directory",
            ],
            out var arguments,
            out var options);

        Assert.Empty(arguments);
        Assert.Equal(
            [
                "-h, -?, --help  Show help and usage",
                "--log-path <PATH>  Enable file logging",
                "to the specified directory",
            ],
            options);
    }

    [Fact]
    public void Infers_Box_Table_Options_From_Mixed_Preamble_In_The_Original_Order()
    {
        var inferred = LegacyOptionTable.InferOptionLines(
            [
                "",
                "dotnet-repl [options]",
                "",
                "┌───────────────────────┬───────────────────────────────┐",
                "│ Option                │ Description                   │",
                "├───────────────────────┼───────────────────────────────┤",
                "│ -h, -?, --help        │ Show help and usage           │",
                "│ --log-path <PATH>     │ Enable file logging           │",
                "│                       │ to the specified directory    │",
                "└───────────────────────┴───────────────────────────────┘",
            ],
            ["dotnet-repl [options]"]);

        Assert.Equal(
            [
                "-h, -?, --help  Show help and usage",
                "--log-path <PATH>  Enable file logging",
                "to the specified directory",
            ],
            inferred);
    }

    [Fact]
    public void Assembles_Box_Table_Option_Lines_In_The_Original_Order()
    {
        var text =
            """
            dotnet-repl

             dotnet-repl [options]

            ┌───────────────────────┬───────────────────────────────┐
            │ Option                │ Description                   │
            ├───────────────────────┼───────────────────────────────┤
            │ -h, -?, --help        │ Show help and usage           │
            │ --log-path <PATH>     │ Enable file logging           │
            │                       │ to the specified directory    │
            └───────────────────────┴───────────────────────────────┘
            """;
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var preamble = new List<string>();
        string? currentSection = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (currentSection is not null
                && !string.Equals(currentSection, "__ignored__", StringComparison.Ordinal)
                && TextNoiseClassifier.ShouldIgnoreSectionLine(line))
            {
                continue;
            }

            if (SectionHeaderSupport.TryParseIgnoredSectionHeader(line, HelpSectionCatalog.IgnoredHeaders))
            {
                currentSection = "__ignored__";
                continue;
            }

            if (SectionHeaderSupport.TryParseSectionHeader(line, HelpSectionCatalog.Aliases, out var sectionName, out _, out _))
            {
                currentSection = sectionName;
                sections.TryAdd(sectionName, []);
                continue;
            }

            if (currentSection is null)
            {
                if (!TextNoiseClassifier.ShouldIgnorePreambleLine(line))
                {
                    preamble.Add(line);
                }
            }
            else if (!string.Equals(currentSection, "__ignored__", StringComparison.Ordinal))
            {
                sections[currentSection].Add(line);
            }
        }

        var (title, _, _) = TitleInference.ParseTitleAndVersion(preamble);
        sections.TryGetValue("usage", out var usageLines);
        var usageSectionParts = UsageSectionSplitter.Split(usageLines ?? []);
        var trailingStructuredBlock = TrailingStructuredBlockInference.Infer(sections);
        var rawArgumentLines = ParserInputAssemblySupport.BuildRawArgumentLines(
            preamble,
            title,
            sections,
            usageSectionParts,
            trailingStructuredBlock);

        ItemParser.SplitArgumentSectionLines(rawArgumentLines, out _, out var optionStyleArgumentLines);

        var parsedUsageLines = usageSectionParts.UsageLines.Count > 0
            ? usageSectionParts.UsageLines.Select(line => line.Trim()).Where(line => line.Length > 0).ToArray()
            : PreambleInference.InferUsageLines(preamble);
        var optionCandidateLines = preamble
            .Skip(string.IsNullOrWhiteSpace(title) ? 0 : 1)
            .Concat(trailingStructuredBlock.OptionLines)
            .ToArray();
        var structuredOptionLines = StructuredOptionTableSupport.TryExtractStructuredOptionLines(optionCandidateLines);
        var seededOptionLines = LegacyOptionTable.InferOptionLines(optionCandidateLines, parsedUsageLines);
        var fullTextInferredOptionLines = LegacyOptionTable.InferOptionLines(lines, parsedUsageLines);

        Assert.Equal(
            [
                "-h, -?, --help  Show help and usage",
                "--log-path <PATH>  Enable file logging",
                "to the specified directory",
            ],
            structuredOptionLines);
        Assert.Equal(
            [
                "-h, -?, --help  Show help and usage",
                "--log-path <PATH>  Enable file logging",
                "to the specified directory",
            ],
            seededOptionLines);
        Assert.Equal(
            [
                "-h, -?, --help  Show help and usage",
                "--log-path <PATH>  Enable file logging",
                "to the specified directory",
            ],
            fullTextInferredOptionLines);

        var rawOptionLines = ParserInputAssemblySupport.BuildRawOptionLines(
            lines,
            preamble,
            title,
            sections,
            parsedUsageLines,
            usageSectionParts,
            trailingStructuredBlock,
            optionStyleArgumentLines,
            rawArgumentLines,
            out _);

        Assert.Equal(
            [
                "-h, -?, --help  Show help and usage",
                "--log-path <PATH>  Enable file logging",
                "to the specified directory",
            ],
            rawOptionLines);
    }

    [Fact]
    public void Builds_Clean_OpenCli_From_Bannered_Help_Documents()
    {
        var parser = new TextParser();
        var builder = new OpenCliBuilder();
        var rootHelp = parser.Parse(
            """
            (\_/)
              ( >_<)  o     forge for .NET Aspire (MVW)
             <|  | )--|     Issues / PRs welcome at github.com/rudiv/forge
              |___|

            USAGE:
                forge [OPTIONS] [COMMAND]

            OPTIONS:
                -h, --help       Prints help information
                -v, --version    Prints version information

            COMMANDS:
                fire    Fire up the AppHost for local development (dotnet watch)

            🧽 Shutting down...
            """);
        var fireHelp = parser.Parse(
            """
            (\_/)
              ( >_<)  o     forge for .NET Aspire (MVW)
             <|  | )--|     Issues / PRs welcome at github.com/rudiv/forge
              |___|

            DESCRIPTION:
            Fire up the AppHost for local development (dotnet watch).

            USAGE:
                forge fire [OPTIONS]

            OPTIONS:
                                              DEFAULT
                -p, --port                    6969       Set the runtime port for the DCP
                                                         Integration host

            🧽 Shutting down...
            """);

        var document = builder.Build(
            "forge",
            "0.1.0-preview.0.18",
            new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = rootHelp,
                ["fire"] = fireHelp,
            });

        Assert.Equal("forge", document["info"]!["title"]!.GetValue<string>());

        var fire = Assert.Single(document["commands"]!.AsArray());
        var port = Assert.Single(fire!["options"]!.AsArray());
        Assert.Equal("--port", port!["name"]!.GetValue<string>());
        Assert.Contains("Integration host", port["description"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("Shutting down", port["description"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Builds_Options_From_Grouped_Bare_Option_Prototype_Blocks_Without_Inventing_Group_Arguments()
    {
        var parser = new TextParser();
        var builder = new OpenCliBuilder();
        var rootHelp = parser.Parse(
            """
            CYCODT - AI-powered CLI Test Framework

            USAGE: cycodt <command> [...]

            COMMANDS

              cycodt list [...]       Lists CLI YAML tests
            """);
        var listHelp = parser.Parse(
            """
            CYCODT LIST

            USAGE: cycodt list [...]

              FILES
                --file FILE
                --files FILE1 [FILE2 [...]]

              TESTS
                --test TEXT
                --tests TEXT1 [TEXT2 [...]]
            """);

        var document = builder.Build(
            "cycodt",
            "1.0.0-alpha",
            new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = rootHelp,
                ["list"] = listHelp,
            });

        var list = Assert.Single(document["commands"]!.AsArray());
        var options = list!["options"]!.AsArray()
            .OfType<JsonObject>()
            .Select(option => option["name"]!.GetValue<string>())
            .ToArray();

        Assert.Equal(new[] { "--file", "--files", "--test", "--tests" }, options);
        Assert.Null(list["arguments"]);
    }

    [Fact]
    public void Does_Not_Invent_Positional_Arguments_For_Option_Only_Commands_That_Shadow_Option_Value_Names()
    {
        var parser = new TextParser();
        var builder = new OpenCliBuilder();
        var rootHelp = parser.Parse(
            """
            demo

            Usage: demo <command>

            Commands:
              check  Validate expectations.
            """);
        var checkHelp = parser.Parse(
            """
            demo check

            Usage: demo check [options]

              INPUT
                --input FILE          Read input from FILE
                --save-output FILE    Write output to FILE
            """);

        var document = builder.Build(
            "demo",
            "1.0.0",
            new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = rootHelp,
                ["check"] = checkHelp,
            });

        var check = Assert.Single(document["commands"]!.AsArray());
        Assert.Null(check!["arguments"]);
        Assert.Equal(
            new[] { "--input", "--save-output" },
            check["options"]!.AsArray().OfType<JsonObject>().Select(option => option["name"]!.GetValue<string>()).ToArray());
    }

    [Fact]
    public void Does_Not_Turn_Recolor_Usage_Prose_And_License_Text_Into_Commands()
    {
        var parser = new TextParser();
        var builder = new OpenCliBuilder();
        var rootHelp = parser.Parse(
            """
            Recolor 2.5.0+6cf9c7646a9301bfc7c6b6ece2c9265ec5039aac)
            Copyright © 2010 Atif Aziz. All rights reserved.
            Portions Copyright © .NET Foundation and Contributors.

            Colors text received over standard input based on regular expression patterns.

            Usage:

                $NAME COLOR1=REGEX1 COLOR2=REGEX2 ... COLORN=REGEXN

            A color is specified in one of three formats:

                1. FOREGROUND
                2. FOREGROUND/BACKGROUND
                3. HEX

            Format 1 sets only the foreground color of the text whereas format 2 sets the
            foreground and background colors (separated by a forward-slash to mean
            foreground over background). FOREGROUND can be omitted to set just the
            background. The colors themselves are specified using the names listed below.
            In format 3, the colors are specified by two hex digits: the first corresponds
            to the background and the second the foreground. If only a single hex digit is
            given then it sets the foreground color. The color corresponding to each hex
            digits is shown below.

                0 = Black           8 = DarkGray
                1 = DarkBlue        9 = Blue
                2 = DarkGreen       A = Green
                3 = DarkCyan        B = Cyan
                4 = DarkRed         C = Red
                5 = DarkMagenta     D = Magenta
                6 = DarkYellow      E = Yellow
                7 = Gray            F = White

            The regular expression pattern language reference can be found online at:

                http://go.microsoft.com/fwlink/?LinkId=133231

            Below is a quick reference:

                Main Elements ------------------------------------------------------------

                text     Matches exact characters anywhere in the original text.

                .        Matches any single character.

                [chars]  Matches at least one of the characters in the brackets.

                [range]  Matches at least one of the characters within the range. The use
                         of a hyphen (-) allows you to specify an adjacent character.

                [^chars] Matches any characters except those in brackets.

                ^        Matches the beginning characters.

                $        Matches the end characters.

                *        Matches any instances of the preceding character.

                ?        Matches zero or one instance of the preceding character.

                \        Matches the character that follows as an escaped character.

                Quantifiers --------------------------------------------------------------

                *        Specifies zero or more matches.

                +        Matches repeating instances of the preceding characters.

                ?        Specifies zero or one matches.

                {n}      Specifies exactly n matches.

                {n,}     Specifies at least n matches.

                {n,m}    Specifies at least n, but no more than m, matches.

                Character Classes --------------------------------------------------------

                \p{name} Matches any character in the named character class specified by
                         {name}. Supported names are Unicode groups and block ranges such
                         as Ll, Nd, Z, IsGreek, and IsBoxDrawing.

            Licensed under the Apache License, Version 2.0 (the "License"); you may not
            use this file except in compliance with the License.

            Portions of this software are covered by The MIT License (MIT):

                Copyright (c) .NET Foundation and Contributors. All rights reserved.

                The above copyright notice and this permission notice shall be
                included in all copies or substantial portions of the Software.
            """);

        var document = builder.Build(
            "recolor",
            "2.5.0",
            new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = rootHelp,
            });

        var commands = document["commands"]?.AsArray().OfType<JsonObject>()
            .Select(command => command["name"]?.GetValue<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray()
            ?? [];

        Assert.True(commands.Length == 0, $"Unexpected commands: {string.Join(", ", commands)}");
        Assert.DoesNotContain("Apache", document.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"above\"", document.ToJsonString(), StringComparison.Ordinal);
    }
}
