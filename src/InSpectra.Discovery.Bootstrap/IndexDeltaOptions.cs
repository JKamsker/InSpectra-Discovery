internal sealed record IndexDeltaOptions
{
    public const string DefaultCurrentSnapshotPath = "state/discovery/dotnet-tools.current.json";
    public const string DefaultDeltaOutputPath = "state/discovery/dotnet-tools.delta.json";
    public const string DefaultCursorStatePath = "state/discovery/dotnet-tools.cursor.json";

    public bool Json { get; init; }
    public string CurrentSnapshotPath { get; init; } = DefaultCurrentSnapshotPath;
    public string DeltaOutputPath { get; init; } = DefaultDeltaOutputPath;
    public string CursorStatePath { get; init; } = DefaultCursorStatePath;
    public string ServiceIndexUrl { get; init; } = BootstrapOptions.DefaultServiceIndexUrl;
    public int Concurrency { get; init; } = 12;
    public int OverlapMinutes { get; init; } = 30;
    public DateTimeOffset? SeedCursorUtc { get; init; }

    public static IndexDeltaOptions Parse(string[] args)
    {
        var options = new IndexDeltaOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--json":
                    options = options with { Json = true };
                    break;
                case "--current":
                    options = options with { CurrentSnapshotPath = ReadValue(args, ref index, arg, options.Json) };
                    break;
                case "--output":
                    options = options with { DeltaOutputPath = ReadValue(args, ref index, arg, options.Json) };
                    break;
                case "--cursor":
                    options = options with { CursorStatePath = ReadValue(args, ref index, arg, options.Json) };
                    break;
                case "--service-index":
                    options = options with { ServiceIndexUrl = ReadValue(args, ref index, arg, options.Json) };
                    break;
                case "--concurrency":
                    options = options with { Concurrency = ReadPositiveInt(args, ref index, arg, options.Json) };
                    break;
                case "--overlap-minutes":
                    options = options with { OverlapMinutes = ReadNonNegativeInt(args, ref index, arg, options.Json) };
                    break;
                case "--seed-cursor-utc":
                    options = options with { SeedCursorUtc = ReadDateTimeOffset(args, ref index, arg, options.Json) };
                    break;
                default:
                    throw new CliUsageException($"Unknown option '{arg}' for 'index delta'.", HelpTopic.IndexDelta, options.Json);
            }
        }

        return options;
    }

    private static string ReadValue(string[] args, ref int index, string argName, bool json)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException($"Expected a value after '{argName}'.", HelpTopic.IndexDelta, json);
        }

        index++;
        return args[index];
    }

    private static int ReadPositiveInt(string[] args, ref int index, string argName, bool json)
    {
        var value = ReadValue(args, ref index, argName, json);
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new CliUsageException($"Expected a positive integer after '{argName}'.", HelpTopic.IndexDelta, json);
    }

    private static int ReadNonNegativeInt(string[] args, ref int index, string argName, bool json)
    {
        var value = ReadValue(args, ref index, argName, json);
        return int.TryParse(value, out var parsed) && parsed >= 0
            ? parsed
            : throw new CliUsageException($"Expected a non-negative integer after '{argName}'.", HelpTopic.IndexDelta, json);
    }

    private static DateTimeOffset ReadDateTimeOffset(string[] args, ref int index, string argName, bool json)
    {
        var value = ReadValue(args, ref index, argName, json);
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.ToUniversalTime()
            : throw new CliUsageException($"Expected an ISO-8601 timestamp after '{argName}'.", HelpTopic.IndexDelta, json);
    }
}
