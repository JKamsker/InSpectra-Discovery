namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json.Nodes;
using Xunit;

public sealed class HookOpenCliBuilderTests
{
    [Fact]
    public void Build_Falls_Back_To_Command_Name_When_Parsed_Title_Is_NonPublishable()
    {
        var capture = CreateCapture("/usr/share/dotnet/dotnet");

        var document = HookOpenCliBuilder.Build("dotnet-iqsharp", "0.28.302812", capture);

        Assert.Equal("dotnet-iqsharp", document["info"]?["title"]?.GetValue<string>());
        Assert.Equal("/usr/share/dotnet/dotnet", document["x-inspectra"]?["cliParsedTitle"]?.GetValue<string>());
    }

    [Fact]
    public void Build_Preserves_Publishable_Parsed_Title()
    {
        var capture = CreateCapture("IQ#");

        var document = HookOpenCliBuilder.Build("dotnet-iqsharp", "0.28.302812", capture);

        Assert.Equal("IQ#", document["info"]?["title"]?.GetValue<string>());
        Assert.Null(document["x-inspectra"]?["cliParsedTitle"]);
    }

    [Fact]
    public void Build_Replaces_Obfuscated_Option_Argument_Name_With_Option_Derived_Name()
    {
        var capture = CreateCapture(
            "Aspose.Page.Convert",
            new HookCapturedOption
            {
                Name = "--project",
                ArgumentName = "#=Z_BSO_H3_QU=",
                ValueType = "String",
                MaxArity = 1,
            });

        var document = HookOpenCliBuilder.Build("Aspose.Page.Convert", "26.2.0", capture);

        var option = Assert.Single(document["options"]!.AsArray());
        var argument = Assert.Single(option!["arguments"]!.AsArray());
        Assert.Equal("PROJECT", argument!["name"]?.GetValue<string>());
    }

    [Fact]
    public void Build_Hoists_Directive_Host_Wrapper_Command_For_Startup_Hook_Captures()
    {
        var capture = new HookCaptureResult
        {
            CliFramework = "System.CommandLine",
            FrameworkVersion = "2.0.0.0",
            PatchTarget = "Parse-postfix",
            Root = new HookCapturedCommand
            {
                Name = "Weik.io CLI",
                Description = "CLI for the Weik.io integration platform",
                Subcommands =
                [
                    new HookCapturedCommand { Name = "#!who", Description = "Host directive" },
                    new HookCapturedCommand
                    {
                        Name = "#!weikio",
                        Description = "Hello Weik.io",
                        Subcommands =
                        [
                            new HookCapturedCommand { Name = "connector", Description = "Integration connector management" },
                            new HookCapturedCommand { Name = "version", Description = "Show the version number" },
                        ],
                    },
                ],
            },
        };

        var document = HookOpenCliBuilder.Build("weikio", "2024.1.0-preview.37", capture);

        Assert.Equal("Weik.io CLI", document["info"]?["title"]?.GetValue<string>());
        var commands = document["commands"]?.AsArray().OfType<JsonObject>().ToArray() ?? [];
        Assert.Equal(
            new[] { "connector", "version" },
            commands.Select(command => command["name"]?.GetValue<string>()).ToArray());
        Assert.DoesNotContain(commands, command => command["name"]?.GetValue<string>()?.StartsWith("#", StringComparison.Ordinal) is true);
    }

    private static HookCaptureResult CreateCapture(string rootName, params HookCapturedOption[] options)
        => new()
        {
            CliFramework = "McMaster.Extensions.CommandLineUtils",
            FrameworkVersion = "2.3.4.0",
            PatchTarget = "Execute-postfix",
            Root = new HookCapturedCommand
            {
                Name = rootName,
                Options = options.Length > 0
                    ? [.. options]
                    : [new HookCapturedOption
                    {
                        Name = "--help",
                    }],
            },
        };
}
