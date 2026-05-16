namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Infrastructure.Commands;

using Xunit;

public sealed class InstalledDotnetToolCommandSupportTests
{
    [Fact]
    public void TryResolve_Returns_Installed_Entry_Point_For_Matching_Command()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var toolDirectory = Path.Combine(tempDirectory.Path, ".store", "demo.tool", "1.2.3", "demo.tool", "1.2.3", "tools", "net8.0", "any");
        Directory.CreateDirectory(toolDirectory);

        var settingsPath = Path.Combine(toolDirectory, "DotnetToolSettings.xml");
        File.WriteAllText(
            settingsPath,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <DotNetCliTool Version="1">
              <Commands>
                <Command Name="demo" EntryPoint="./Demo.Tool.dll" Runner="dotnet" />
              </Commands>
            </DotNetCliTool>
            """);

        var command = InstalledDotnetToolCommandSupport.TryResolve(tempDirectory.Path, "demo");

        Assert.NotNull(command);
        Assert.Equal("demo", command!.CommandName);
        Assert.Equal("dotnet", command.Runner);
        Assert.Equal(Path.Combine(toolDirectory, "Demo.Tool.dll"), command.EntryPointPath);
        Assert.Equal(settingsPath, command.SettingsPath);
    }
}
