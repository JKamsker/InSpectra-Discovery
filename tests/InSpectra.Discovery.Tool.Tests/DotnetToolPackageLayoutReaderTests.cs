namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Packages;

using System.IO.Compression;
using Xunit;

public sealed class DotnetToolPackageLayoutReaderTests
{
    [Fact]
    public void Read_Collects_Tool_Settings_Commands_And_Entry_Points()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var archivePath = Path.Combine(tempDirectory.Path, "sample.zip");

        using (var createArchive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = createArchive.CreateEntry("tools/net8.0/any/DotnetToolSettings.xml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(
                """
                <DotNetCliTool>
                  <Commands>
                    <Command Name="sample" EntryPoint="sample.dll" />
                  </Commands>
                </DotNetCliTool>
                """);
        }

        using var readArchive = ZipFile.OpenRead(archivePath);
        var layout = DotnetToolPackageLayoutReader.Read(readArchive);

        Assert.Equal(["tools/net8.0/any/DotnetToolSettings.xml"], layout.ToolSettingsPaths);
        Assert.Equal(["sample"], layout.ToolCommandNames);
        Assert.Equal(["tools/net8.0/any/sample.dll"], layout.ToolEntryPointPaths);
        Assert.Contains("tools/net8.0/any", layout.ToolDirectories);
    }
}

