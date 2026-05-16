namespace InSpectra.Discovery.Tool.Packages;

using System.IO.Compression;

internal static class DotnetToolPackageLayoutReader
{
    public static DotnetToolPackageLayout Read(ZipArchive archive)
    {
        var builder = new DotnetToolPackageLayoutBuilder();

        foreach (var entry in archive.Entries)
        {
            if (!string.Equals(entry.Name, "DotnetToolSettings.xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = entry.Open();
            builder.Add(entry.FullName, DotnetToolSettingsReader.Read(stream, entry.FullName));
        }

        return builder.Build();
    }
}

