using Xunit;

public sealed class ToolHelpTextParserTests
{
    [Fact]
    public void Parses_CliFx_Style_Help_Text()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            husky v0.9.1

            USAGE
              husky [options]
              husky [command] [...]

            OPTIONS
              -h|--help         Shows help text.
              --version         Shows version information.

            COMMANDS
              add               Add husky hook
              install           Install Husky hooks
            """);

        Assert.Equal("husky", document.Title);
        Assert.Equal("v0.9.1", document.Version);
        Assert.Equal(2, document.UsageLines.Count);
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-h|--help", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "add", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "install", StringComparison.Ordinal));
    }

    [Fact]
    public void Parses_Colon_Sections_With_Multiline_Descriptions()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            dotnet-serve 1.10.194

            Usage: dotnet serve [options]

            Options:
              -d|--directory <DIR>   The root directory to serve.
                                      Supports relative paths.
              -v|--verbose           Show more console output.
            """);

        Assert.Equal("dotnet-serve", document.Title);
        Assert.Single(document.UsageLines);
        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "-d|--directory <DIR>", StringComparison.Ordinal)
            && option.Description!.Contains("Supports relative paths.", StringComparison.Ordinal));
    }

    [Fact]
    public void Parses_Localized_Section_Headers()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            VERWENDUNG:
                dotnet cake [SCRIPT] [OPTIONEN]

            ARGUMENTE:
                [SCRIPT]    The Cake script. Defaults to build.cake

            OPTIONEN:
                -v, --verbosity <VERBOSITY>  Specifies the amount of information to be displayed.
            """);

        Assert.Contains("dotnet cake [SCRIPT] [OPTIONEN]", document.UsageLines);
        Assert.Single(document.Arguments);
        Assert.Equal("SCRIPT", document.Arguments[0].Key);
        Assert.Single(document.Options);
        Assert.Equal("-v, --verbosity <VERBOSITY>", document.Options[0].Key);
    }

    [Fact]
    public void Parses_Subcommands_Section_Alias()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Paket version 10.3.1

            USAGE: paket [<subcommand> [<options>]]

            SUBCOMMANDS:

                add <options>         add a new dependency
                install <options>     compute dependency graph
            """);

        Assert.Single(document.UsageLines);
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "add", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "install", StringComparison.Ordinal));
    }

    [Fact]
    public void Does_Not_Treat_Wrapped_Command_Descriptions_As_New_Commands()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Paket version 10.3.1

            SUBCOMMANDS:

                show-conditions <options>
                                      show conditions defined on groups
                simplify <options>    simplify declared dependencies
            """);

        Assert.Contains(document.Commands, command => string.Equals(command.Key, "show-conditions", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "simplify", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Commands, command => string.Equals(command.Key, "show conditions defined on groups", StringComparison.Ordinal));
    }

    [Fact]
    public void Falls_Back_To_Indented_Command_List_When_No_Commands_Header_Exists()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            snapx 10.0.0+67eae04d993a714128cefbb77cc80fed8b0f7fc3
            Copyright © Finter As

              demote     Demote one or multiple releases
              promote    Promote a snap to next release channel
              pack       Publish a new release
              help       Display more information on a specific command.
            """);

        Assert.Contains(document.Commands, command => string.Equals(command.Key, "demote", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "promote", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "pack", StringComparison.Ordinal));
    }

    [Fact]
    public void Infers_Options_From_Preamble_Without_Options_Header()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Pickles version 0.0.0.0
              -f, --feature-directory=VALUE
                                         directory to start scanning recursively for
                                           features
              -o, --output-directory=VALUE
                                         directory where output files will be placed
              -h, -?, --help
            """);

        Assert.Contains(document.Options, option => string.Equals(option.Key, "-f, --feature-directory=VALUE", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-h, -?, --help", StringComparison.Ordinal));
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void Does_Not_Parse_Later_Stack_Trace_Lines_As_Title_And_Version()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Azure B2C Console Client
            ========================
            Configuration is missing or incomplete. Let's set it up:

            Error: The authority (including the tenant ID) must be in a well-formed URI format.  (Parameter 'authority')
            Details: System.ArgumentException: The authority (including the tenant ID) must be in a well-formed URI format.  (Parameter 'authority')
               at B2CConsoleClient.AuthenticationService..ctor(AuthConfig config) in /Users/test/B2CConsoleClient/AuthenticationService.cs:line 31
               at B2CConsoleClient.Program.Main(String[] args) in /Users/test/B2CConsoleClient/Program.cs:line 19
            """);

        Assert.Equal("Azure B2C Console Client", document.Title);
        Assert.Null(document.Version);
    }

    [Fact]
    public void Infers_Usage_From_Preamble_Without_Usage_Section_Header()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            DependenSee

            Usage - DependenSee <SourceFolder> [<OutputPath>] -options

            GlobalOption                       Description
            Help (-H)                          Shows help descriptions.
            """);

        Assert.Single(document.UsageLines);
        Assert.Equal("DependenSee <SourceFolder> [<OutputPath>] -options", document.UsageLines[0]);
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void Infers_Legacy_Option_Table_Without_Classifying_Usage_Arguments_As_Options()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Usage - DependenSee <SourceFolder> [<OutputPath>] -options

            GlobalOption                       Description
            Help (-H)                          Shows help descriptions.
            SourceFolder* (-S)                 Root folder.
            OutputPath (-O)                    Output path.
            IncludePackages (-P)               Include packages.
            """);

        Assert.Contains(document.Options, option => string.Equals(option.Key, "-H, --help", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-P, --include-packages", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Options, option => option.Key.Contains("source-folder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(document.Options, option => option.Key.Contains("output-path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Normalizes_Command_Keys_By_Removing_Usage_Placeholders()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Usage: dotnet-trace [command] [options]

            Commands:
              convert <input-filename>  Converts traces to alternate formats.
              report <trace_filename>   Generates a report.
            """);

        Assert.Contains(document.Commands, command => string.Equals(command.Key, "convert", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "report", StringComparison.Ordinal));
    }

    [Fact]
    public void Parses_Markdown_Option_Tables_Without_Inferring_Fake_Commands()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            RegisterBot Version 2.0.20.0

            ```RegisterBot [--endpoint endpoint] [--name botName] [--resource-group groupName] [--help]```

            Creates or updates a bot registration for [botName] pointing to [endpoint] with teams channel and SSO enabled.

            | Argument                         | Description                                                                                   |
            | -------------------------------- | --------------------------------------------------------------------------------------------- |
            | -e, --endpoint endpoint          | (optional) If not specified the endpoint will stay the same as project settings               |
            | -n, --name botName               | (optional) If not specified the botname will be pulled from settings or interactively asked   |
            | -g, --resource-group groupName   | (optional) If not specified the groupname will be pulled from settings or interactively asked |
            | -v, --verbose                    | (optional) show all commands as they are executed                                             |
            | -h, --help                       | display help                                                                                  |

            If the endpoint host name is:

            | Host                 | Action                                                                               |
            | -------------------- | ------------------------------------------------------------------------------------ |
            | xx.azurewebsites.net | it modifies the remote web app settings to have correct settings/secrets             |
            | localhost            | it modifies the local project settings/user secrets to have correct settings/secrets |
            """);

        Assert.Contains(document.UsageLines, line => string.Equals(line, "RegisterBot [--endpoint endpoint] [--name botName] [--resource-group groupName] [--help]", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-e, --endpoint <ENDPOINT>", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-n, --name <BOTNAME>", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-g, --resource-group <GROUPNAME>", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-v, --verbose", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-h, --help", StringComparison.Ordinal));
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void Ignores_Bare_Pipe_Lines_When_Inferring_Options()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            | Gitfo v0.4.0
            |
            | No .gitfo config found, scanning local folders for Git repos...
            | No local Git repos found.
            gitfo 0.0.0.0
            Copyright 2022-2025

              -p, --profile       Profile to use

              -l, --scan-local    Ignore .gitfo config and scan local folders

              --help              Display this help screen.

              --version           Display version information.
            """);

        Assert.Contains(document.Options, option => string.Equals(option.Key, "-p, --profile", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-l, --scan-local", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "--help", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "--version", StringComparison.Ordinal));
    }

    [Fact]
    public void Does_Not_Infer_Ascii_Banner_As_Commands()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Ola mundo!
            .NET Tool criada com .NET 9...

             .----------------.  .----------------.
            | .--------------. || .--------------. |
            | |    ______    | || |  _______     | |
            | |  .' ___  |   | || | |_   __ \    | |
            | '--------------' || '--------------' |
             '----------------'  '----------------'
            """);

        Assert.Empty(document.Commands);
        Assert.Empty(document.Options);
    }

    [Fact]
    public void Does_Not_Parse_Stack_Trace_Separators_As_Options()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Unhandled exception. System.Net.Sockets.SocketException: Unknown socket error
               at Program.Main(String[] args) in Program.cs:line 59
            --- End of stack trace from previous location ---
               at Spectre.Console.Status.StartAsync(String status) in Status.cs:line 117
            """);

        Assert.Empty(document.Options);
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void Parses_Box_Drawing_Option_Tables_Without_Inferring_Fake_Commands()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
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
            """);

        Assert.Contains(document.Options, option => string.Equals(option.Key, "-h, -?, --help", StringComparison.Ordinal));
        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "--log-path <PATH>", StringComparison.Ordinal)
            && option.Description!.Contains("specified directory", StringComparison.Ordinal));
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void Does_Not_Infer_Commands_From_Log_Prefixes_Or_Register_Dumps()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            ExactOnline OpenApiGenerator
            By Stef Heyenrath
            info: Usage: obfuscar [Options] <project_file>
            info:   -h, --help  Show help information
              r8: 00002a00000b4263  r9: 0000000000000001
              dx: 0000000000000006  ax: 0000000000000000
            """);

        Assert.Empty(document.Commands);
        Assert.Empty(document.Options);
    }

    [Fact]
    public void Normalizes_Trailing_Colon_In_Commands_Section()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Swashbuckle (Swagger) Command Line Tools
            Commands:
              tofile:  retrieves Swagger from a startup assembly, and writes to file
              list:    retrieves the list of Swagger document names from a startup assembly
            """);

        Assert.Contains(document.Commands, command => string.Equals(command.Key, "tofile", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "list", StringComparison.Ordinal));
    }

    [Fact]
    public void Ignores_CommandLineParser_Error_Preamble_And_Pseudo_Verbs()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Error parsing
             CommandLine.HelpVerbRequestedError
            GSoft.CertificateTool 1.0.0+bb4d252c46ae13f3169853b02995b8cd77635ab6
            Copyright (C) 2026 GSoft.CertificateTool

              add        Installs a pfx certificate to selected store.
              remove     Removes a pfx certificate from selected store.
              version    Display version information.
            """);

        Assert.Equal("GSoft.CertificateTool", document.Title);
        Assert.Equal("1.0.0+bb4d252c46ae13f3169853b02995b8cd77635ab6", document.Version);
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "add", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "remove", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "version", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Commands, command => string.Equals(command.Key, "CommandLine.HelpVerbRequestedError", StringComparison.Ordinal));
    }

    [Fact]
    public void Treats_Singular_Command_Header_As_Command_Specific_Help()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Apizr dedicated version of NSwag command line tool for Net70, toolchain v13.19.0.0
            Visit http://NSwag.org for more information.
            NSwag bin directory: /tmp/tool

            Command: run
              Executes an .nswag file. If 'input' is not specified then all *.nswag files are executed.

            Arguments:
              Variables
                Variables passed to the command.

            Duration: 00:00:00.0549972
            """);

        Assert.Equal("run", document.Title);
        Assert.Equal("Executes an .nswag file. If 'input' is not specified then all *.nswag files are executed.", document.CommandDescription);
        Assert.Empty(document.Commands);
        Assert.Single(document.Arguments);
        Assert.Equal("Variables", document.Arguments[0].Key);
        Assert.Equal("Variables passed to the command.", document.Arguments[0].Description);
    }

    [Fact]
    public void Ignores_Raw_Output_And_Redirection_Warning_Sections()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            CLI Version: 7.0.0

            Description:
              Fetch the current account

            Usage:
              Beamable.Tools me [options]

            Options:
              -?, -h, --help  Show help and usage information

            Raw Output:
              Returns a stream of AccountMeCommandOutput object on the stream stream.
              {
                "ts": 1774693724241
              }

            Redirection Warning:
              The quiet flag must be used.
            """);

        Assert.Equal("Fetch the current account", document.CommandDescription);
        Assert.Single(document.Options);
        Assert.Equal("-?, -h, --help", document.Options[0].Key);
        Assert.Equal("Show help and usage information", document.Options[0].Description);
        Assert.DoesNotContain("Raw Output", document.Options[0].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalizes_Comma_Separated_Command_Aliases_To_First_Alias()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Usage: Beamable.Tools [command] [options]

            Commands:
              deploy, deployment, deployments, deploys  Commands for deployments
              otel, tel, telemetry                      Open telemetry commands
            """);

        Assert.Contains(document.Commands, command => string.Equals(command.Key, "deploy", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "otel", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Commands, command => command.Key.Contains(',', StringComparison.Ordinal));
    }

    [Fact]
    public void Ignores_Bracketed_Structured_Log_Prefixes_When_Resolving_Title()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            [12:52:57 INF] AttackSurfaceAnalyzer v.2.3.331+569f4d0249
            Asa 2.3.331+569f4d0249
            © Microsoft Corporation. All rights reserved.

              collect           Collect operating system metrics
              monitor           Continue running and monitor activity
            """);

        Assert.Equal("Asa", document.Title);
        Assert.Equal("2.3.331+569f4d0249", document.Version);
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "collect", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Commands, command => command.Key.StartsWith("[12:52:57", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_Explicit_Help_Switch_Rejection_Preambles()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            --help is an unknown parameter
            Usage of the tool (argument names case insensitive, values case insensitive where marked, arguments can be given in any order):
            octo-ckc [-[shortTerm] or [/ or --][longTerm] [argument value]] ...
            """);

        Assert.False(document.HasContent);
        Assert.Null(document.Title);
        Assert.Empty(document.Options);
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void Does_Not_Infer_Commands_From_DotnetToolList_Headers()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            UpdateAllDotnetTools 1.0.0

            Package Id               Version      Commands
            ------------------------------------------------
            updatealldotnettools     1.0.0        UpdateAllDotnetTools
            """);

        Assert.Equal("UpdateAllDotnetTools", document.Title);
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void Does_Not_Infer_Commands_From_Template_Install_Headers()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            RapidFire CLI

            Template Name            Short Name   Language   Tags
            ---------------------------------------------------------
            rapidfire-api            rf-api       [C#]       Web/API
            """);

        Assert.Equal("RapidFire CLI", document.Title);
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void Reattaches_Pipe_Separated_Long_Aliases_To_Option_Signatures()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Amazon Deploy Tools

            Options:
              -pl                           | --project-location             The location of the project.
              -cfg                          | --config-file                  Configuration file storing defaults.
            """);

        Assert.Contains(document.Options, option => string.Equals(option.Key, "-pl | --project-location", StringComparison.Ordinal));
        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "-cfg | --config-file", StringComparison.Ordinal)
            && string.Equals(option.Description, "Configuration file storing defaults.", StringComparison.Ordinal));
    }

    [Fact]
    public void Does_Not_Start_New_Command_From_Indented_Wrapped_Description_Or_Help_Hints()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            USAGE: Propulsion.Tool [--help] <subcommand> [<options>]

            SUBCOMMANDS:

                init <options>        Initialize auxiliary store (Supported for `cosmos`
                                      Only).
                initpg <options>      Initialize a postgres checkpoint store

                Use 'Propulsion.Tool <subcommand> --help' for additional information.
            """);

        var init = Assert.Single(document.Commands.Where(command => string.Equals(command.Key, "init", StringComparison.Ordinal)));
        Assert.Contains("Only).", init.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(document.Commands, command => string.Equals(command.Key, "Only).", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Commands, command => string.Equals(command.Key, "Use", StringComparison.Ordinal));
    }
}
