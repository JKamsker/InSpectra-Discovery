using System.IO.Compression;
using Xunit;

public sealed class PackageArchivePathSupportTests
{
    [Fact]
    public void NormalizeArchivePath_Resolves_Dot_Segments()
    {
        var path = PackageArchivePathSupport.NormalizeArchivePath("tools/net8.0/any", "../shared/./sample.dll");

        Assert.Equal("tools/net8.0/shared/sample.dll", path);
    }

    [Fact]
    public void IsToolManagedAssembly_Matches_Tool_Directories_And_Excludes_Framework_Assemblies()
    {
        using var tempDirectory = new TestTemporaryDirectory();
        var archivePath = Path.Combine(tempDirectory.Path, "sample.zip");

        using (var createArchive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            createArchive.CreateEntry("tools/net8.0/any/sample.dll");
            createArchive.CreateEntry("tools/net8.0/any/Spectre.Console.dll");
        }

        using var readArchive = ZipFile.OpenRead(archivePath);
        var toolDirectories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "tools/net8.0/any" };
        var sampleEntry = readArchive.GetEntry("tools/net8.0/any/sample.dll") ?? throw new InvalidOperationException("Missing sample entry.");
        var spectreEntry = readArchive.GetEntry("tools/net8.0/any/Spectre.Console.dll") ?? throw new InvalidOperationException("Missing Spectre entry.");

        Assert.True(PackageArchivePathSupport.IsToolManagedAssembly(sampleEntry, toolDirectories, "Spectre.Console.dll"));
        Assert.False(PackageArchivePathSupport.IsToolManagedAssembly(spectreEntry, toolDirectories, "Spectre.Console.dll"));
    }
}
