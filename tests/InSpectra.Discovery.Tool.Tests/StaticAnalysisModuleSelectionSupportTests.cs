namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

using Xunit;

public sealed class StaticAnalysisModuleSelectionSupportTests
{
    [Fact]
    public void SelectPaths_Prefers_Entry_Point_When_Framework_Dll_Is_Only_Framework_Match()
    {
        var entryPointPath = Path.Combine("tool", "Aspose.PSD.CLI.NLP-Editor.dll");
        ScannedModuleMetadata[] modules =
        [
            new ScannedModuleMetadata(
                Path.Combine("tool", "CommandLine.dll"),
                "CommandLine",
                []),
            new ScannedModuleMetadata(
                entryPointPath,
                "Aspose.PSD.CLI.NLP-Editor",
                ["System.Runtime", "System.Console"]),
        ];

        var selected = StaticAnalysisModuleSelectionSupport.SelectPaths(modules, "CommandLine", entryPointPath);

        Assert.Equal([entryPointPath], selected);
    }

    [Fact]
    public void SelectPaths_Uses_Non_Framework_Modules_When_Framework_References_Are_Available()
    {
        ScannedModuleMetadata[] modules =
        [
            new ScannedModuleMetadata(
                Path.Combine("tool", "CommandLine.dll"),
                "CommandLine",
                []),
            new ScannedModuleMetadata(
                Path.Combine("tool", "Demo.Tool.dll"),
                "Demo.Tool",
                ["System.Runtime"]),
            new ScannedModuleMetadata(
                Path.Combine("tool", "Demo.Commands.dll"),
                "Demo.Commands",
                ["CommandLine", "System.Runtime"]),
        ];

        var selected = StaticAnalysisModuleSelectionSupport.SelectPaths(
            modules,
            "CommandLine",
            Path.Combine("tool", "Demo.Tool.dll"));

        Assert.Equal([Path.Combine("tool", "Demo.Commands.dll")], selected);
    }

    [Fact]
    public void SelectPaths_Orders_Preferred_Entry_Point_First_When_It_Also_References_Framework()
    {
        var preferredPath = Path.Combine("tool", "Demo.Tool.dll");
        ScannedModuleMetadata[] modules =
        [
            new ScannedModuleMetadata(
                Path.Combine("tool", "Demo.Commands.dll"),
                "Demo.Commands",
                ["CommandLine", "System.Runtime"]),
            new ScannedModuleMetadata(
                preferredPath,
                "Demo.Tool",
                ["CommandLine", "System.Runtime"]),
        ];

        var selected = StaticAnalysisModuleSelectionSupport.SelectPaths(modules, "CommandLine", preferredPath);

        Assert.Equal(
            [preferredPath, Path.Combine("tool", "Demo.Commands.dll")],
            selected);
    }
}
