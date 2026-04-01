namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.OpenCli;
using InSpectra.Discovery.Tool.Help.Parsing;

using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliBuilderTests
{
    [Fact]
    public void Builds_OpenCli_From_Help_Documents_Only()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.2.3",
                ApplicationDescription: "Demo CLI",
                CommandDescription: null,
                UsageLines: ["demo [command] [options]"],
                Arguments: [],
                Options:
                [
                    new Item("-v|--verbose", false, "Verbose output"),
                    new Item("-c, --config <PATH>", false, "Path to config"),
                ],
                Commands:
                [
                    new Item("user add", false, "Add a user"),
                ]),
            ["user add"] = new(
                Title: "demo",
                Version: "1.2.3",
                ApplicationDescription: null,
                CommandDescription: "Add a user",
                UsageLines: ["demo user add <NAME> [--admin]"],
                Arguments: [],
                Options:
                [
                    new Item("--admin", false, "Grant admin"),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.2.3", helpDocuments);

        Assert.Equal("demo", document["info"]!["title"]!.GetValue<string>());
        Assert.Equal("1.2.3", document["info"]!["version"]!.GetValue<string>());

        var rootOptions = document["options"]!.AsArray();
        Assert.Equal("--config", rootOptions[1]!["name"]!.GetValue<string>());
        Assert.Equal("-c", rootOptions[1]!["aliases"]![0]!.GetValue<string>());
        Assert.Equal("PATH", rootOptions[1]!["arguments"]![0]!["name"]!.GetValue<string>());

        var user = document["commands"]![0]!.AsObject();
        Assert.Equal("user", user["name"]!.GetValue<string>());

        var add = user["commands"]![0]!.AsObject();
        Assert.Equal("add", add["name"]!.GetValue<string>());
        Assert.Equal("NAME", add["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("--admin", add["options"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Prefers_More_Descriptive_Single_Dash_Alias_As_Primary_Name()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options:
                [
                    new Item("-i, -input <PATH>", false, "Input file"),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var option = document["options"]![0]!;

        Assert.Equal("-input", option["name"]!.GetValue<string>());
        Assert.Equal("-i", option["aliases"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Omits_Null_Collections_And_Does_Not_Emit_Option_Level_Required()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options:
                [
                    new Item("--verbose", false, null),
                    new Item("--config <PATH>", false, "Path to config"),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var options = document["options"]!.AsArray();

        Assert.Null(document["arguments"]);
        Assert.Null(document["info"]!["description"]);
        Assert.Null(options[0]!["required"]);
        Assert.Null(options[0]!["description"]);
        Assert.Null(options[1]!["required"]);
        Assert.Equal("PATH", options[1]!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.True(options[1]!["arguments"]![0]!["required"]!.GetValue<bool>());
    }

    [Fact]
    public void Does_Not_Emit_Subcommand_Usage_Placeholders_As_Arguments()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["demo <subcommand> [<options>]"],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new Item("sync", false, "Synchronize data"),
                ]),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);

        Assert.Null(document["arguments"]);
        Assert.Single(document["commands"]!.AsArray());
    }

    [Fact]
    public void Sanitizes_Polluted_Argument_Names_And_Variadic_Placeholders()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments:
                [
                    new Item("FUNCTION-NAME> THE NAME OF THE FUNCTION TO INVOKE", true, null),
                    new Item("THE PROJECT TO USE.", false, null),
                    new Item("<search_pattern1 search_pattern2 ...>", false, null),
                ],
                Options: [],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var arguments = document["arguments"]!.AsArray();

        Assert.Equal(2, arguments.Count);
        Assert.Equal("FUNCTION_NAME", arguments[0]!["name"]!.GetValue<string>());
        Assert.Equal("SEARCH_PATTERN", arguments[1]!["name"]!.GetValue<string>());
        Assert.Equal(0, arguments[1]!["arity"]!["minimum"]!.GetValue<int>());
        Assert.Null(arguments[1]!["arity"]!["maximum"]);
    }

    [Fact]
    public void Skips_Option_Shaped_Placeholders_And_Normalizes_Choice_Metavars()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["demo [options] <--path|-p=<START_PATH>>"],
                Arguments:
                [
                    new Item("--format-json", false, "Bad positional"),
                ],
                Options:
                [
                    new Item("--path|-p=<START_PATH>", false, "Start path"),
                    new Item("-f, --format <Csv|Json|Table|Yaml>", false, "Output format"),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var options = document["options"]!.AsArray();

        Assert.Null(document["arguments"]);
        Assert.Equal("START_PATH", options[0]!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("FORMAT", options[1]!["arguments"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Does_Not_Emit_Dispatcher_Target_As_Root_Argument_When_Child_Commands_Exist()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "abp",
                Version: "10.2.0-dev",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["abp <command> <target> [options]"],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new Item("add-module", false, "Add a multi-package module."),
                ]),
        };

        var document = builder.Build("abp", "10.2.0-dev", helpDocuments);

        Assert.Null(document["arguments"]);
        Assert.Single(document["commands"]!.AsArray());
    }

    [Fact]
    public void Does_Not_Emit_Option_Metavars_As_Usage_Only_Positional_Arguments()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["demo [--path <PATH>] [--format <Csv|Json|Table|Yaml>] <PROJECT>"],
                Arguments: [],
                Options: [],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var arguments = document["arguments"]!.AsArray();

        var project = Assert.Single(arguments);
        Assert.Equal("PROJECT", project!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Uses_Package_Metadata_Version_Over_Banner_Version()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options: [],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.4", helpDocuments);

        Assert.Equal("1.0.4", document["info"]!["version"]!.GetValue<string>());
    }

    [Fact]
    public void Infers_Required_Value_Arguments_Without_Adding_Them_To_Flag_Like_Descriptions()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "snapx",
                Version: "10.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments:
                [
                    new Item("Model name", true, "The name of the model"),
                ],
                Options:
                [
                    new Item("--producers", false, "Required. List of producers to include."),
                    new Item("--rid", false, "Required. Runtime identifier (RID), e.g win-x64."),
                    new Item("--release", false, "Required. Force release lock specified in snapx.yml."),
                    new Item("--from-version", false, "Remove all releases newer than this version."),
                    new Item("--trace", false, "Enables task execution tracing."),
                    new Item("--webpart", false, "Required. Creates a webpart configuration."),
                    new Item("--print-config", false, "Print path to config file"),
                    new Item("--package-file", false, "Generates UTF-8 text file with package metadata."),
                    new Item("--resolvedir", false, "Additional assembly resolve directories."),
                    new Item("--keyfile", false, "Sign rewritten assembly with this key file."),
                    new Item("--exclude", false, "Comma separated change types to exclude."),
                    new Item("--assembly", false, "Required."),
                    new Item("--target-directory", false, null),
                    new Item("--version", false, "Display version information"),
                    new Item("--rc|restore-concurrency", false, "(Default: 4) The number of concurrent restores."),
                ],
                Commands: []),
        };

        var document = builder.Build("snapx", "10.0.0", helpDocuments);
        var options = document["options"]!.AsArray();
        var arguments = document["arguments"]!.AsArray();

        var producers = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--producers", StringComparison.Ordinal)));
        Assert.Equal("PRODUCERS", producers!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.True(producers["arguments"]![0]!["required"]!.GetValue<bool>());
        Assert.Equal("List of producers to include.", producers["description"]!.GetValue<string>());

        var rid = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--rid", StringComparison.Ordinal)));
        Assert.Equal("RID", rid!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.True(rid["arguments"]![0]!["required"]!.GetValue<bool>());

        var release = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--release", StringComparison.Ordinal)));
        Assert.Null(release!["arguments"]);
        Assert.Equal("Force release lock specified in snapx.yml.", release["description"]!.GetValue<string>());

        var fromVersion = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--from-version", StringComparison.Ordinal)));
        Assert.Equal("FROM_VERSION", fromVersion!["arguments"]![0]!["name"]!.GetValue<string>());

        var trace = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--trace", StringComparison.Ordinal)));
        Assert.Null(trace!["arguments"]);

        var webpart = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--webpart", StringComparison.Ordinal)));
        Assert.Null(webpart!["arguments"]);
        Assert.Equal("Creates a webpart configuration.", webpart["description"]!.GetValue<string>());

        var printConfig = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--print-config", StringComparison.Ordinal)));
        Assert.Null(printConfig!["arguments"]);

        var packageFile = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--package-file", StringComparison.Ordinal)));
        Assert.Null(packageFile!["arguments"]);

        var resolveDir = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--resolvedir", StringComparison.Ordinal)));
        Assert.Equal("RESOLVEDIR", resolveDir!["arguments"]![0]!["name"]!.GetValue<string>());

        var keyFile = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--keyfile", StringComparison.Ordinal)));
        Assert.Equal("KEYFILE", keyFile!["arguments"]![0]!["name"]!.GetValue<string>());

        var exclude = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--exclude", StringComparison.Ordinal)));
        Assert.Equal("EXCLUDE", exclude!["arguments"]![0]!["name"]!.GetValue<string>());

        var assembly = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--assembly", StringComparison.Ordinal)));
        Assert.Equal("ASSEMBLY", assembly!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.True(assembly["arguments"]![0]!["required"]!.GetValue<bool>());

        var targetDirectory = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--target-directory", StringComparison.Ordinal)));
        Assert.Equal("TARGET_DIRECTORY", targetDirectory!["arguments"]![0]!["name"]!.GetValue<string>());

        var version = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--version", StringComparison.Ordinal)));
        Assert.Null(version!["arguments"]);

        var restoreConcurrency = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--restore-concurrency", StringComparison.Ordinal)));
        Assert.Equal("--rc", restoreConcurrency!["aliases"]![0]!.GetValue<string>());
        Assert.Equal("RESTORE_CONCURRENCY", restoreConcurrency["arguments"]![0]!["name"]!.GetValue<string>());

        var modelName = Assert.Single(arguments);
        Assert.Equal("MODEL_NAME", modelName!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Emits_Arguments_For_Bare_Word_Option_Metavars()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "nake",
                Version: "4.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options:
                [
                    new Item("--runner NAME", false, "Runner to execute"),
                ],
                Commands: []),
        };

        var document = builder.Build("nake", "4.0.0", helpDocuments);
        var option = document["options"]![0]!;

        Assert.Equal("--runner", option["name"]!.GetValue<string>());
        Assert.Equal("NAME", option["arguments"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Prefers_Root_Command_Description_Over_Preamble_Description()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "adfs",
                Version: "1.0.1",
                ApplicationDescription: "No parameters file found in the current directory.",
                CommandDescription: "ADFS Authentication CLI tool",
                UsageLines: [],
                Arguments: [],
                Options: [],
                Commands: []),
        };

        var document = builder.Build("adfs", "1.0.1", helpDocuments);

        Assert.Equal("ADFS Authentication CLI tool", document["info"]!["description"]!.GetValue<string>());
    }

    [Fact]
    public void Infers_Value_Arguments_From_CommandLineParser_Descriptions()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "refresh",
                Version: "1.3.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options:
                [
                    new Item("-p, --Project", false, "Project to be refactored"),
                    new Item("-m, --Migration", false, "Required. Path to migration file"),
                    new Item("-h, --host", false, "(Default: localhost) Address/host to listen at."),
                    new Item("--help", false, "Display this help screen."),
                ],
                Commands: []),
        };

        var document = builder.Build("refresh", "1.3.0", helpDocuments);
        var options = document["options"]!.AsArray();

        var project = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--Project", StringComparison.Ordinal)));
        Assert.Equal("PROJECT", project!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.False(project["arguments"]![0]!["required"]!.GetValue<bool>());

        var migration = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--Migration", StringComparison.Ordinal)));
        Assert.Equal("MIGRATION", migration!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.True(migration["arguments"]![0]!["required"]!.GetValue<bool>());
        Assert.Equal("Path to migration file", migration["description"]!.GetValue<string>());

        var host = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--host", StringComparison.Ordinal)));
        Assert.Equal("HOST", host!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.False(host["arguments"]![0]!["required"]!.GetValue<bool>());

        var help = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--help", StringComparison.Ordinal)));
        Assert.Null(help!["arguments"]);
    }

    [Fact]
    public void Does_Not_Infer_Value_Arguments_For_Flag_Descriptions_That_Mention_Nouns()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options:
                [
                    new Item("-a, --add", false, "Add default using namespaces"),
                    new Item("-r, --recursive", false, "(Default: false) Recursively process specified directory."),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var options = document["options"]!.AsArray();

        var add = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--add", StringComparison.Ordinal)));
        Assert.Null(add!["arguments"]);

        var recursive = Assert.Single(options.Where(option => string.Equals(option?["name"]?.GetValue<string>(), "--recursive", StringComparison.Ordinal)));
        Assert.Null(recursive!["arguments"]);
    }

    [Fact]
    public void Does_Not_Emit_Command_Inventory_Echo_As_Builtin_Help_Arguments()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new Item("pack", false, "Creates a new package."),
                    new Item("help", false, "Display more information on a specific command."),
                ]),
            ["help"] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: "Display more information on a specific command.",
                UsageLines: [],
                Arguments:
                [
                    new Item("pack", false, "Creates a new package."),
                    new Item("help", false, "Display more information on a specific command."),
                    new Item("version", false, "Display version information."),
                ],
                Options:
                [
                    new Item(
                        "--help",
                        false,
                        "Display this help screen.\npack (pos. 0) Creates a new package.\nversion (pos. 1) Display version information."),
                ],
                Commands:
                [
                    new Item("pack", false, "Creates a new package."),
                    new Item("help", false, "Display more information on a specific command."),
                    new Item("version", false, "Display version information."),
                ]),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var help = Assert.Single(document["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "help", StringComparison.Ordinal)));

        Assert.Null(help!["arguments"]);
    }

    [Fact]
    public void Preserves_Command_Descriptions_From_Root_Inventory_When_Subcommand_Help_Lacks_Them()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "feval",
                Version: "1.7.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new Item("config", false, "Config feval command line tool"),
                ]),
            ["config"] = new(
                Title: "feval",
                Version: "1.7.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options:
                [
                    new Item("-l, --list", false, "List all configurations"),
                ],
                Commands: []),
        };

        var document = builder.Build("feval", "1.7.0", helpDocuments);
        var config = Assert.Single(document["commands"]!.AsArray());

        Assert.Equal("Config feval command line tool", config!["description"]!.GetValue<string>());
    }

    [Fact]
    public void Does_Not_Emit_Generic_File_Argument_For_Intermediate_Dispatchers_With_Child_Commands()
    {
        var parser = new TextParser();
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = parser.Parse(
                """
                Usage: ncp [command]

                NetCorePal.Cloud.CLI.Toolkit

                Commands:
                  new    ??????

                Options:
                  --completion    Generate a shell completion code
                  -h, --help      Show help message
                  --version       Show version
                """),
            ["new"] = parser.Parse(
                """
                Usage: ncp new [command]

                NetCorePal.Cloud.CLI.Toolkit

                Commands:
                  ar      ?????`{name}.cs`??
                  cmd     ????`{name}Command.cs`??,?????????
                  repo    ????`{name}Repository.cs`??
                  de      ??????`{name}DomainEvent.cs`??
                  deh     ?????????`{name}.cs`??

                Options:
                  -h, --help    Show help message
                """),
        };

        var document = builder.Build("ncp", "0.1.1", helpDocuments);
        var rootCommands = Assert.IsType<JsonArray>(document["commands"]);
        var newCommand = Assert.IsType<JsonObject>(Assert.Single(rootCommands));

        Assert.Equal("new", newCommand["name"]!.GetValue<string>());
        Assert.Null(newCommand["arguments"]);

        var childCommands = Assert.IsType<JsonArray>(newCommand["commands"]);
        Assert.Equal(
            new[] { "ar", "cmd", "de", "deh", "repo" },
            childCommands
                .OfType<JsonObject>()
                .Select(command => command["name"]!.GetValue<string>())
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void Builds_CommandScoped_Subcommand_Surfaces_From_A_Single_Root_Help_Page()
    {
        var rootHelp = new TextParser().Parse(
            """
            SqlDatabase Command Line Tools (v6.0.2)
            Project on https://github.com/Apps72/Dev.Data
            Usage: DbCmd <command> [options]

            Commands:
              GenerateEntities   | ge     Generate a file with entities.
              Merge              | mg     Merge all script files.
              Run                | rn     Run all script files.

            'GenerateEntities' options:
              --ConnectionString | -cs    Required. Connection string to the database server.
              --Output           | -o     File name where class will be written.

            'Merge' options:
              --Source           | -s     Source directory pattern containing all files to merge.

            Example:
              DbCmd GenerateEntities -cs=\"Server=localhost;Database=Scott;\" -o=Entities.cs
            """);
        var builder = new OpenCliBuilder();
        var document = builder.Build(
            "DbCmd",
            "6.0.2",
            new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = rootHelp,
            });

        Assert.Equal("SqlDatabase Command Line Tools", document["info"]!["title"]!.GetValue<string>());
        Assert.Null(document["options"]);

        var commands = document["commands"]!.AsArray();
        Assert.DoesNotContain(commands, command => string.Equals(command?["name"]?.GetValue<string>(), "Example", StringComparison.Ordinal));

        var generateEntities = Assert.Single(commands.Where(command => string.Equals(command?["name"]?.GetValue<string>(), "GenerateEntities", StringComparison.Ordinal)));
        Assert.Equal("--ConnectionString", generateEntities!["options"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("-cs", generateEntities["options"]![0]!["aliases"]![0]!.GetValue<string>());

        var merge = Assert.Single(commands.Where(command => string.Equals(command?["name"]?.GetValue<string>(), "Merge", StringComparison.Ordinal)));
        Assert.Equal("--Source", merge!["options"]![0]!["name"]!.GetValue<string>());

        var run = Assert.Single(commands.Where(command => string.Equals(command?["name"]?.GetValue<string>(), "Run", StringComparison.Ordinal)));
        Assert.Null(run!["options"]);
    }
}
