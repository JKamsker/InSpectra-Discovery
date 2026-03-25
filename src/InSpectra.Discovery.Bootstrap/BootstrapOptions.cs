internal sealed class BootstrapOptions
{
    public const string DefaultOutputPath = "artifacts/index/dotnet-tools.current.json";
    public const string DefaultPrefixAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    public const string DefaultServiceIndexUrl = "https://api.nuget.org/v3/index.json";

    public string OutputPath { get; init; } = DefaultOutputPath;
    public string PrefixAlphabet { get; init; } = DefaultPrefixAlphabet;
    public string ServiceIndexUrl { get; init; } = DefaultServiceIndexUrl;
    public int PageSize { get; init; } = 1000;
    public int MetadataConcurrency { get; init; } = 12;

    public static BootstrapOptions Parse(string[] args)
    {
        var options = new BootstrapOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            switch (arg)
            {
                case "--help":
                case "-h":
                    throw new ArgumentException("Help requested.");
                case "--output":
                    options = options.WithOutputPath(ReadValue(args, ref index, arg));
                    break;
                case "--prefix-alphabet":
                    options = options.WithPrefixAlphabet(ReadValue(args, ref index, arg));
                    break;
                case "--service-index":
                    options = options.WithServiceIndex(ReadValue(args, ref index, arg));
                    break;
                case "--page-size":
                    options = options.WithPageSize(ReadPositiveInt(args, ref index, arg));
                    break;
                case "--concurrency":
                    options = options.WithConcurrency(ReadPositiveInt(args, ref index, arg));
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        return options;
    }

    public static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: dotnet run --project src/InSpectra.Discovery.Bootstrap -- [options]");
        writer.WriteLine("  --output <path>            Output JSON path.");
        writer.WriteLine("  --concurrency <number>     Metadata fetch concurrency. Default: 12.");
        writer.WriteLine("  --page-size <number>       Autocomplete page size. Default: 1000.");
        writer.WriteLine("  --prefix-alphabet <chars>  Prefix alphabet used for ID enumeration.");
        writer.WriteLine("  --service-index <url>      NuGet V3 service index URL.");
    }

    private BootstrapOptions WithConcurrency(int value) => new()
    {
        OutputPath = OutputPath,
        PrefixAlphabet = PrefixAlphabet,
        ServiceIndexUrl = ServiceIndexUrl,
        PageSize = PageSize,
        MetadataConcurrency = value,
    };

    private BootstrapOptions WithOutputPath(string value) => new()
    {
        OutputPath = value,
        PrefixAlphabet = PrefixAlphabet,
        ServiceIndexUrl = ServiceIndexUrl,
        PageSize = PageSize,
        MetadataConcurrency = MetadataConcurrency,
    };

    private BootstrapOptions WithPageSize(int value) => new()
    {
        OutputPath = OutputPath,
        PrefixAlphabet = PrefixAlphabet,
        ServiceIndexUrl = ServiceIndexUrl,
        PageSize = value,
        MetadataConcurrency = MetadataConcurrency,
    };

    private BootstrapOptions WithPrefixAlphabet(string value) => new()
    {
        OutputPath = OutputPath,
        PrefixAlphabet = value,
        ServiceIndexUrl = ServiceIndexUrl,
        PageSize = PageSize,
        MetadataConcurrency = MetadataConcurrency,
    };

    private BootstrapOptions WithServiceIndex(string value) => new()
    {
        OutputPath = OutputPath,
        PrefixAlphabet = PrefixAlphabet,
        ServiceIndexUrl = value,
        PageSize = PageSize,
        MetadataConcurrency = MetadataConcurrency,
    };

    private static int ReadPositiveInt(string[] args, ref int index, string argName)
    {
        var value = ReadValue(args, ref index, argName);
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new ArgumentException($"Expected a positive integer after '{argName}'.");
    }

    private static string ReadValue(string[] args, ref int index, string argName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Expected a value after '{argName}'.");
        }

        index++;
        return args[index];
    }
}
