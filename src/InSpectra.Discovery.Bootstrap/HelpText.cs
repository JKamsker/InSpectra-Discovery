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
            case HelpTopic.IndexDelta:
                WriteIndexDelta(writer);
                break;
            case HelpTopic.IndexDeltaSpectreConsoleCli:
                WriteIndexDeltaSpectreConsoleCli(writer);
                break;
            case HelpTopic.Filter:
                WriteFilter(writer);
                break;
            case HelpTopic.FilterSpectreConsole:
                WriteFilterSpectreConsole(writer);
                break;
            case HelpTopic.FilterSpectreConsoleCli:
                WriteFilterSpectreConsoleCli(writer);
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
        writer.WriteLine("  Discover added or updated dotnet tools since the last catalog cursor:");
        writer.WriteLine("    dotnet run --project src/InSpectra.Discovery.Bootstrap -- index delta");
        writer.WriteLine();
        writer.WriteLine("  Narrow the latest delta to changed Spectre.Console.Cli tools and queue them:");
        writer.WriteLine("    dotnet run --project src/InSpectra.Discovery.Bootstrap -- index delta-spectre-console-cli");
        writer.WriteLine();
        writer.WriteLine("  Write the Spectre.Console subset from an existing index:");
        writer.WriteLine("    dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console");
        writer.WriteLine();
        writer.WriteLine("  Write the Spectre.Console.Cli subset from an existing index:");
        writer.WriteLine("    dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console-cli");
        writer.WriteLine();
        writer.WriteLine("COMMANDS");
        writer.WriteLine("  index build              Build the current ranked dotnet-tool index from NuGet.");
        writer.WriteLine("  index delta              Discover added or updated dotnet tools since the saved catalog cursor.");
        writer.WriteLine("  index delta-spectre-console-cli");
        writer.WriteLine("                           Narrow the latest delta to packages with Spectre.Console.Cli evidence.");
        writer.WriteLine("  filter spectre-console   Filter an index to packages with Spectre.Console evidence.");
        writer.WriteLine("  filter spectre-console-cli");
        writer.WriteLine("                           Filter an index to packages with Spectre.Console.Cli evidence.");
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

    private static void WriteIndexDelta(TextWriter writer)
    {
        writer.WriteLine("NAME");
        writer.WriteLine("  index delta");
        writer.WriteLine();
        writer.WriteLine("USAGE");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- index delta [options]");
        writer.WriteLine();
        writer.WriteLine("DESCRIPTION");
        writer.WriteLine("  Walks the NuGet catalog from the saved cursor, finds dotnet-tool package IDs whose");
        writer.WriteLine("  effective latest listed version changed, updates the current snapshot in place, and");
        writer.WriteLine("  writes a delta JSON file for the changed package IDs.");
        writer.WriteLine();
        writer.WriteLine("OPTIONS");
        writer.WriteLine($"  --current <path>           Current snapshot path. Default: {IndexDeltaOptions.DefaultCurrentSnapshotPath}");
        writer.WriteLine($"  --output <path>            Delta output JSON path. Default: {IndexDeltaOptions.DefaultDeltaOutputPath}");
        writer.WriteLine($"  --cursor <path>            Cursor state JSON path. Default: {IndexDeltaOptions.DefaultCursorStatePath}");
        writer.WriteLine($"  --service-index <url>      NuGet service index. Default: {BootstrapOptions.DefaultServiceIndexUrl}");
        writer.WriteLine("  --concurrency <number>     Catalog leaf and registration fetch concurrency. Default: 12.");
        writer.WriteLine("  --overlap-minutes <num>    Rescan overlap window to catch boundary races. Default: 30.");
        writer.WriteLine("  --seed-cursor-utc <time>   Seed cursor timestamp if no cursor state exists yet.");
        writer.WriteLine("  --json                     Emit a machine-readable command summary to stdout.");
        writer.WriteLine();
        writer.WriteLine("EXAMPLES");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- index delta");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- index delta --seed-cursor-utc 2026-03-25T13:07:00Z --json");
    }

    private static void WriteIndexDeltaSpectreConsoleCli(TextWriter writer)
    {
        writer.WriteLine("NAME");
        writer.WriteLine("  index delta-spectre-console-cli");
        writer.WriteLine();
        writer.WriteLine("USAGE");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- index delta-spectre-console-cli [options]");
        writer.WriteLine();
        writer.WriteLine("DESCRIPTION");
        writer.WriteLine("  Reads the latest dotnet-tool delta, inspects only the changed package IDs for");
        writer.WriteLine("  Spectre.Console.Cli evidence, writes a narrowed delta snapshot, and emits a");
        writer.WriteLine("  compact queue JSON for current package versions that still need analysis.");
        writer.WriteLine();
        writer.WriteLine("OPTIONS");
        writer.WriteLine($"  --input <path>         Input delta path. Default: {IndexDeltaSpectreConsoleCliOptions.DefaultInputDeltaPath}");
        writer.WriteLine($"  --output <path>        Narrowed delta output path. Default: {IndexDeltaSpectreConsoleCliOptions.DefaultOutputDeltaPath}");
        writer.WriteLine($"  --queue-output <path>  Queue output path. Default: {IndexDeltaSpectreConsoleCliOptions.DefaultQueueOutputPath}");
        writer.WriteLine("  --concurrency <num>    Catalog fetch concurrency. Default: 12.");
        writer.WriteLine("  --json                 Emit a machine-readable command summary to stdout.");
        writer.WriteLine();
        writer.WriteLine("EXAMPLES");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- index delta-spectre-console-cli");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- index delta-spectre-console-cli --json");
    }

    private static void WriteFilter(TextWriter writer)
    {
        writer.WriteLine("NAME");
        writer.WriteLine("  filter");
        writer.WriteLine();
        writer.WriteLine("USAGE");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter <command> [options]");
        writer.WriteLine();
        writer.WriteLine("COMMANDS");
        writer.WriteLine("  spectre-console       Filter an index to packages with Spectre.Console evidence.");
        writer.WriteLine("  spectre-console-cli   Filter an index to packages with Spectre.Console.Cli evidence.");
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
        writer.WriteLine($"  --output <path>        Output JSON path. Default: {SpectreConsoleFilterOptions.DefaultSpectreConsoleOutputPath}");
        writer.WriteLine("  --concurrency <num>    Catalog fetch concurrency. Default: 16.");
        writer.WriteLine("  --json                 Emit a machine-readable command summary to stdout.");
        writer.WriteLine();
        writer.WriteLine("EXAMPLES");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console --output artifacts/index/spectre.json --json");
    }

    private static void WriteFilterSpectreConsoleCli(TextWriter writer)
    {
        writer.WriteLine("NAME");
        writer.WriteLine("  filter spectre-console-cli");
        writer.WriteLine();
        writer.WriteLine("USAGE");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console-cli [options]");
        writer.WriteLine();
        writer.WriteLine("DESCRIPTION");
        writer.WriteLine("  Reads the ranked dotnet-tool index, fetches each package's catalog entry, and");
        writer.WriteLine("  writes a filtered JSON file containing only packages whose catalog payload shows");
        writer.WriteLine("  Spectre.Console.Cli evidence.");
        writer.WriteLine();
        writer.WriteLine("OPTIONS");
        writer.WriteLine($"  --input <path>         Input index path. Default: {SpectreConsoleFilterOptions.DefaultInputPath}");
        writer.WriteLine($"  --output <path>        Output JSON path. Default: {SpectreConsoleFilterOptions.DefaultSpectreConsoleCliOutputPath}");
        writer.WriteLine("  --concurrency <num>    Catalog fetch concurrency. Default: 16.");
        writer.WriteLine("  --json                 Emit a machine-readable command summary to stdout.");
        writer.WriteLine();
        writer.WriteLine("EXAMPLES");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console-cli");
        writer.WriteLine("  dotnet run --project src/InSpectra.Discovery.Bootstrap -- filter spectre-console-cli --output artifacts/index/spectre-cli.json --json");
    }
}
