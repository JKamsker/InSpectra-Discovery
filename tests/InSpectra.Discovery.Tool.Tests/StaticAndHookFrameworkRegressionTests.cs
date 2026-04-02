namespace InSpectra.Discovery.Tool.Tests
{
    using InSpectra.Discovery.Tool.StaticAnalysis.Attributes;
    using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

    using dnlib.DotNet;

    using Xunit;

    public sealed class StaticAndHookFrameworkRegressionTests
    {
        [Fact]
        public void CommandLineParserReader_Reads_Field_And_Inherited_Members()
        {
            using var module = LoadCurrentTestModule();

            var reader = new CmdParserAttributeReader();
            var commands = reader.Read([new ScannedModule(module.Location!, module)]);

            Assert.True(commands.TryGetValue("deploy", out var command));
            Assert.NotNull(command);
            Assert.Contains(command!.Options, option => string.Equals(option.LongName, "force", StringComparison.Ordinal));
            Assert.Contains(command.Options, option => string.Equals(option.LongName, "config", StringComparison.Ordinal));
            Assert.Contains(command.Values, value => string.Equals(value.Name, "TARGET", StringComparison.Ordinal));
        }

        [Fact]
        public void CommandLineParserHeuristicReader_Recovers_Aspose_Like_Parse_Options()
        {
            using var module = LoadCurrentTestModule();

            var typeDef = module.Find(
                "InSpectra.Discovery.Tool.Tests.CommandLineFixtures.AsposeLikeParseOptions",
                isReflectionName: false);
            Assert.NotNull(typeDef);

            var definition = CmdParserHeuristicReaderSupport.ReadDefinition(typeDef!);

            Assert.NotNull(definition);
            Assert.True(definition!.IsDefault);
            Assert.Contains(definition.Options, option => string.Equals(option.LongName, "verbose", StringComparison.Ordinal));
            Assert.Contains(definition.Options, option => string.Equals(option.LongName, "setup", StringComparison.Ordinal));
            Assert.Contains(definition.Options, option => string.Equals(option.LongName, "command", StringComparison.Ordinal));
            Assert.Contains(definition.Options, option => string.Equals(option.LongName, "license-path", StringComparison.Ordinal));
        }

        [Fact]
        public void SystemCommandLineReader_Reads_Private_Options_And_Arguments_From_Command_Types()
        {
            using var module = LoadCurrentTestModule();

            var reader = new SystemCommandLineAttributeReader();
            var commands = reader.Read([new ScannedModule(module.Location!, module)]);

            Assert.True(commands.TryGetValue(string.Empty, out var rootCommand));
            Assert.NotNull(rootCommand);
            Assert.Contains(rootCommand!.Options, option => string.Equals(option.LongName, "verbose", StringComparison.Ordinal));
            Assert.Contains(rootCommand.Options, option => string.Equals(option.LongName, "retry-count", StringComparison.Ordinal));
            Assert.Contains(rootCommand.Values, argument => string.Equals(argument.Name, "input", StringComparison.Ordinal));
        }

        [Fact]
        public void SystemCommandLineReader_Reads_Constructor_Configured_Options_And_Arguments()
        {
            using var module = LoadCurrentTestModule();

            var reader = new SystemCommandLineAttributeReader();
            var commands = reader.Read([new ScannedModule(module.Location!, module)]);

            Assert.True(commands.TryGetValue("constructorconfigured", out var command));
            Assert.NotNull(command);
            Assert.Contains(command!.Options, option => string.Equals(option.LongName, "image", StringComparison.Ordinal));
            Assert.Contains(command.Options, option => string.Equals(option.LongName, "engine", StringComparison.Ordinal));
            Assert.Contains(command.Options, option => string.Equals(option.ShortName?.ToString(), "t", StringComparison.Ordinal));
            Assert.Contains(command.Values, argument => string.Equals(argument.Name, "project", StringComparison.Ordinal));
        }

        [Fact]
        public void SystemCommandLineReader_Reads_Fluent_Factory_Built_Root_And_Subcommands()
        {
            using var module = LoadCurrentTestModule();

            var reader = new SystemCommandLineAttributeReader();
            var commands = reader.Read([new ScannedModule(module.Location!, module)]);

            Assert.True(commands.TryGetValue(string.Empty, out var rootCommand));
            Assert.NotNull(rootCommand);
            Assert.Equal("Factory root description.", rootCommand!.Description);
            Assert.Contains(rootCommand.Options, option => string.Equals(option.LongName, "verbose", StringComparison.Ordinal));

            Assert.True(commands.TryGetValue("sync", out var syncCommand));
            Assert.NotNull(syncCommand);
            Assert.Equal("Sync data.", syncCommand!.Description);
            Assert.Contains(syncCommand.Options, option => string.Equals(option.LongName, "source", StringComparison.Ordinal));
            Assert.Contains(syncCommand.Values, argument => string.Equals(argument.Name, "path", StringComparison.Ordinal));
        }

        [Fact]
        public void CommandLineParserTreeWalker_Builds_Verb_Root_From_TypeInfo_Choices()
        {
            var parseResult = new FakeCommandLineParserResult(new FakeCommandLineParserTypeInfo
            {
                Choices =
                [
                    typeof(CommandLineFixtures.CommandLineParserDeployVerb),
                    typeof(CommandLineFixtures.CommandLineParserStatusVerb),
                ],
            });

            var success = global::CommandLineParserTreeWalker.TryWalk(parseResult, out var root);

            Assert.True(success);
            Assert.NotNull(root);
            Assert.Contains(root!.Subcommands, command => string.Equals(command.Name, "deploy", StringComparison.Ordinal));
            Assert.Contains(root.Subcommands, command => string.Equals(command.Name, "status", StringComparison.Ordinal));

            var deploy = Assert.Single(root.Subcommands.Where(command => string.Equals(command.Name, "deploy", StringComparison.Ordinal)));
            Assert.Contains(deploy.Options, option => string.Equals(option.Name, "--force", StringComparison.Ordinal));
            Assert.Contains(deploy.Options, option => string.Equals(option.Name, "--help", StringComparison.Ordinal));
            Assert.Contains(deploy.Options, option => string.Equals(option.Name, "--version", StringComparison.Ordinal));
            Assert.Contains(deploy.Arguments, argument => string.Equals(argument.Name, "TARGET", StringComparison.Ordinal));
        }

        [Fact]
        public void CommandLineParserTreeWalker_Uses_Heuristic_Parse_Options_When_Attributes_Are_Missing()
        {
            var parseResult = new FakeCommandLineParserResult(new FakeCommandLineParserTypeInfo
            {
                Current = typeof(CommandLineFixtures.AsposeLikeParseOptions),
            });

            var success = global::CommandLineParserTreeWalker.TryWalk(parseResult, out var root);

            Assert.True(success);
            Assert.NotNull(root);
            Assert.Contains(root!.Options, option => string.Equals(option.Name, "--verbose", StringComparison.Ordinal));
            Assert.Contains(root.Options, option => string.Equals(option.Name, "--setup", StringComparison.Ordinal));
            Assert.Contains(root.Options, option => string.Equals(option.Name, "--command", StringComparison.Ordinal));
            Assert.Contains(root.Options, option => string.Equals(option.Name, "--license-path", StringComparison.Ordinal));
            Assert.Contains(root.Options, option => string.Equals(option.Name, "--help", StringComparison.Ordinal));
            Assert.Contains(root.Options, option => string.Equals(option.Name, "--version", StringComparison.Ordinal));
        }

        private static ModuleDefMD LoadCurrentTestModule()
            => ModuleDefMD.Load(
                typeof(StaticAndHookFrameworkRegressionTests).Assembly.Location,
                new ModuleCreationOptions { TryToLoadPdbFromDisk = false });

        private sealed class FakeCommandLineParserResult(FakeCommandLineParserTypeInfo typeInfo)
        {
            public FakeCommandLineParserTypeInfo TypeInfo { get; } = typeInfo;
        }

        private sealed class FakeCommandLineParserTypeInfo
        {
            public Type? Current { get; init; }

            public IReadOnlyList<Type> Choices { get; init; } = [];
        }
    }
}

namespace InSpectra.Discovery.Tool.Tests.CommandLineFixtures
{
    public abstract class CommandLineParserVerbBase
    {
        [global::CommandLine.Option("config", HelpText = "Config path.")]
        public string? Config { get; set; }
    }

    [global::CommandLine.Verb("deploy", HelpText = "Deploy package.")]
    public sealed class CommandLineParserDeployVerb : CommandLineParserVerbBase
    {
        [global::CommandLine.Option('f', "force", HelpText = "Force deployment.", Required = true)]
        public bool Force;

        [global::CommandLine.Value(0, MetaName = "TARGET", HelpText = "Deployment target.", Required = true)]
        public string? Target;
    }

    [global::CommandLine.Verb("status", HelpText = "Show deployment status.")]
    public sealed class CommandLineParserStatusVerb
    {
    }

    public sealed class AsposeLikeParseOptions : AsposeLikeBaseParseOptions
    {
        public bool Verbose { get; set; }

        public string? Setup { get; set; }

        public string? Command { get; set; }
    }

    public abstract class AsposeLikeBaseParseOptions
    {
        public string? LicensePath { get; set; }
    }

    public sealed class StaticReaderRootCommand : global::System.CommandLine.RootCommand
    {
        private readonly global::System.CommandLine.Option<bool> _verboseOption = new();
        private readonly global::System.CommandLine.Option<int> _retryCountOption = new();
        private readonly global::System.CommandLine.Argument<string> _inputArgument = new();
    }

    public sealed class ConstructorConfiguredCommand : global::System.CommandLine.Command
    {
        public ConstructorConfiguredCommand()
        {
            var imageOption = new global::System.CommandLine.Option<string>("--image", "Image name.");
            var engineOption = new global::System.CommandLine.Option<string>(new[] { "-t", "--engine" }, "Container engine.");
            var verboseOption = new global::System.CommandLine.Option<bool>("--verbose");
            var projectArgument = new global::System.CommandLine.Argument<string>("project", "Project path.");

            Add(projectArgument);
            Add(imageOption);
            Add(engineOption);
            Add(verboseOption);
        }
    }

    public static class FluentFactoryCommandBuilder
    {
        public static global::System.CommandLine.RootCommand Build()
        {
            var root = new global::System.CommandLine.RootCommand("Factory root description.");
            var verboseOption = new global::System.CommandLine.Option<bool>("--verbose", "Verbose logging.");
            var syncCommand = new global::System.CommandLine.Command("sync", "Sync data.");
            var sourceOption = new global::System.CommandLine.Option<string>("--source", "Source path.");
            var pathArgument = new global::System.CommandLine.Argument<string>("path", "Target path.");

            syncCommand.Add(sourceOption);
            syncCommand.Add(pathArgument);
            root.Add(verboseOption);
            root.Add(syncCommand);
            return root;
        }
    }
}

namespace CommandLine
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class VerbAttribute(string name) : Attribute
    {
        public string Name { get; } = name;

        public string? HelpText { get; set; }

        public bool Hidden { get; set; }

        public bool IsDefault { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class OptionAttribute : Attribute
    {
        public OptionAttribute(string longName)
        {
            LongName = longName;
        }

        public OptionAttribute(char shortName, string longName)
        {
            ShortName = shortName;
            LongName = longName;
        }

        public string? LongName { get; }

        public char? ShortName { get; }

        public string? HelpText { get; set; }

        public bool Required { get; set; }

        public bool Hidden { get; set; }

        public string? MetaValue { get; set; }

        public object? Default { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ValueAttribute(int index) : Attribute
    {
        public int Index { get; } = index;

        public string? MetaName { get; set; }

        public string? HelpText { get; set; }

        public bool Required { get; set; }

        public object? Default { get; set; }
    }
}

namespace System.CommandLine
{
    public class Command
    {
        public Command()
        {
        }

        public Command(string name, string? description = null)
        {
        }

        public void Add<T>(Option<T> option)
        {
        }

        public void Add<T>(Argument<T> argument)
        {
        }

        public void Add(Command command)
        {
        }

        public void AddOption<T>(Option<T> option)
        {
        }

        public void AddArgument<T>(Argument<T> argument)
        {
        }
    }

    public class RootCommand : Command
    {
        public RootCommand()
        {
        }

        public RootCommand(string? description)
        {
        }
    }

    public class Option<T>
    {
        public Option()
        {
        }

        public Option(string alias, string? description = null)
        {
        }

        public Option(string[] aliases, string? description = null)
        {
        }
    }

    public class Argument<T>
    {
        public Argument()
        {
        }

        public Argument(string name, string? description = null)
        {
        }
    }
}
