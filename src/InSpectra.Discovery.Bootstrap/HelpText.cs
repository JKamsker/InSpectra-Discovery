using System.Reflection;

internal static class HelpText
{
    public static void Write(TextWriter writer, HelpTopic topic)
    {
        switch (topic)
        {
            case HelpTopic.Root:
                WriteRoot(writer);
                break;
            case HelpTopic.IndexBuild:
                WriteIndexBuild(writer);
                break;
            case HelpTopic.FilterSpectreConsole:
                WriteFilterSpectreConsole(writer);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(topic), topic, null);
        }
    }

    public static void WriteVersion(TextWriter writer)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        writer.WriteLine(version);
    }

    private static void WriteRoot(TextWriter writer)
    {
        writer.WriteLine("NAME");
        writer.WriteLine("  InSpectra.Discovery.Bootstrap");
        writer.WriteLine();
        writer.WriteLine("USAGE");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- <command> [options]");
        writer.WriteLine();
        writer.WriteLine("DESCRIPTION");
        writer.WriteLine("  Builds and derives NuGet .NET tool discovery indexes.");
        writer.WriteLine();
        writer.WriteLine("COMMON TASKS");
        writer.WriteLine("  Build the ranked current dotnet-tool index:");
        writer.WriteLine("    dotnet run --project src/InSpectra.Discovery.Bootstrap -- index build");
        writer.WriteLine();
        writer.WriteLine("  Write the Spectre.Console subset from an existing index:");
        writer.WriteLine("    dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console");
        writer.WriteLine();
        writer.WriteLine("COMMANDS");
        writer.WriteLine("  index build              Build the current ranked dotnet-tool index from NuGet.");
        writer.WriteLine("  filter spectre-console   Filter an index to packages with Spectre.Console evidence.");
        writer.WriteLine();
        writer.WriteLine("OPTIONS");
        writer.WriteLine("  -h, --help     Show help.");
        writer.WriteLine("  -V, --version  Show the CLI version.");
    }

    private static void WriteIndexBuild(TextWriter writer)
    {
        writer.WriteLine("NAME");
        writer.WriteLine("  index build");
        writer.WriteLine();
        writer.WriteLine("USAGE");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- index build [options]");
        writer.WriteLine();
        writer.WriteLine("DESCRIPTION");
        writer.WriteLine("  Enumerates current dotnet-tool package IDs from NuGet autocomplete, enriches");
        writer.WriteLine("  them with registration metadata and download counts, and writes a ranked JSON index.");
        writer.WriteLine();
        writer.WriteLine("OPTIONS");
        writer.WriteLine($"  --output <path>            Output JSON path. Default: {BootstrapOptions.DefaultOutputPath}");
        writer.WriteLine("  --concurrency <number>     Metadata fetch concurrency. Default: 12.");
        writer.WriteLine("  --page-size <number>       Autocomplete page size. Default: 1000.");
        writer.WriteLine($"  --prefix-alphabet <chars>  Prefix alphabet. Default: {BootstrapOptions.DefaultPrefixAlphabet}");
        writer.WriteLine($"  --service-index <url>      NuGet service index. Default: {BootstrapOptions.DefaultServiceIndexUrl}");
        writer.WriteLine("  --json                     Emit a machine-readable command summary to stdout.");
        writer.WriteLine();
        writer.WriteLine("EXAMPLES");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- index build");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- index build --output artifacts/index/tools.json --json");
    }

    private static void WriteFilterSpectreConsole(TextWriter writer)
    {
        writer.WriteLine("NAME");
        writer.WriteLine("  filter spectre-console");
        writer.WriteLine();
        writer.WriteLine("USAGE");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console [options]");
        writer.WriteLine();
        writer.WriteLine("DESCRIPTION");
        writer.WriteLine("  Reads the ranked dotnet-tool index, fetches each package's catalog entry, and");
        writer.WriteLine("  writes a filtered JSON file containing only packages whose catalog payload shows");
        writer.WriteLine("  Spectre.Console or Spectre.Console.Cli evidence.");
        writer.WriteLine();
        writer.WriteLine("OPTIONS");
        writer.WriteLine($"  --input <path>         Input index path. Default: {SpectreConsoleFilterOptions.DefaultInputPath}");
        writer.WriteLine($"  --output <path>        Output JSON path. Default: {SpectreConsoleFilterOptions.DefaultOutputPath}");
        writer.WriteLine("  --concurrency <num>    Catalog fetch concurrency. Default: 16.");
        writer.WriteLine("  --json                 Emit a machine-readable command summary to stdout.");
        writer.WriteLine();
        writer.WriteLine("EXAMPLES");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console --output artifacts/index/spectre.json --json");
    }
}
