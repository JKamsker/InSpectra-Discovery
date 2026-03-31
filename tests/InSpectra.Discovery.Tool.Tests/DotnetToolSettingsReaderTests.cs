namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Packages;

using System.Text;
using Xunit;

public sealed class DotnetToolSettingsReaderTests
{
    [Fact]
    public void Read_Extracts_Command_Names_And_Normalized_Entry_Points()
    {
        var xml = """
            <DotNetCliTool>
              <Commands>
                <Command Name="sample" EntryPoint="./sample.dll" />
                <Command Name="sample-admin" EntryPoint="../shared/admin.dll" />
              </Commands>
            </DotNetCliTool>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var document = DotnetToolSettingsReader.Read(stream, "tools/net8.0/any/DotnetToolSettings.xml");

        Assert.Equal("tools/net8.0/any", document.ToolDirectory);
        Assert.Equal(["sample", "sample-admin"], document.Commands.Select(command => command.CommandName));
        Assert.Equal(
            ["tools/net8.0/any/sample.dll", "tools/net8.0/shared/admin.dll"],
            document.Commands.Select(command => command.EntryPointPath));
    }

    [Fact]
    public void Read_Falls_Back_To_Top_Level_Command_Metadata()
    {
        var xml = """
            <DotNetCliTool>
              <ToolCommandName>sample</ToolCommandName>
              <EntryPoint>sample.dll</EntryPoint>
            </DotNetCliTool>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var document = DotnetToolSettingsReader.Read(stream, "tools/net8.0/any/DotnetToolSettings.xml");
        var command = Assert.Single(document.Commands);

        Assert.Equal("sample", command.CommandName);
        Assert.Equal("tools/net8.0/any/sample.dll", command.EntryPointPath);
    }
}

