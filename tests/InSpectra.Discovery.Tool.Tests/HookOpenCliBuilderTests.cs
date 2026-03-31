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

    private static HookCaptureResult CreateCapture(string rootName)
        => new()
        {
            CliFramework = "McMaster.Extensions.CommandLineUtils",
            FrameworkVersion = "2.3.4.0",
            PatchTarget = "Execute-postfix",
            Root = new HookCapturedCommand
            {
                Name = rootName,
                Options =
                [
                    new HookCapturedOption
                    {
                        Name = "--help",
                    },
                ],
            },
        };
}
