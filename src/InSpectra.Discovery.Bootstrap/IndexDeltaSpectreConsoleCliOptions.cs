internal sealed record IndexDeltaSpectreConsoleCliOptions
{
    public const string DefaultInputDeltaPath = "state/discovery/dotnet-tools.delta.json";
    public const string DefaultOutputDeltaPath = "state/discovery/dotnet-tools.spectre-console-cli.delta.json";
    public const string DefaultQueueOutputPath = "state/discovery/dotnet-tools.spectre-console-cli.queue.json";

    public bool Json { get; init; }
    public string InputDeltaPath { get; init; } = DefaultInputDeltaPath;
    public string OutputDeltaPath { get; init; } = DefaultOutputDeltaPath;
    public string QueueOutputPath { get; init; } = DefaultQueueOutputPath;
    public int Concurrency { get; init; } = 12;

    public static IndexDeltaSpectreConsoleCliOptions Parse(string[] args)
    {
        var options = new IndexDeltaSpectreConsoleCliOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--json":
                    options = options with { Json = true };
                    break;
                case "--input":
                    options = options with { InputDeltaPath = ReadValue(args, ref index, arg, options.Json) };
                    break;
                case "--output":
                    options = options with { OutputDeltaPath = ReadValue(args, ref index, arg, options.Json) };
                    break;
                case "--queue-output":
                    options = options with { QueueOutputPath = ReadValue(args, ref index, arg, options.Json) };
                    break;
                case "--concurrency":
                    options = options with { Concurrency = ReadPositiveInt(args, ref index, arg, options.Json) };
                    break;
                default:
                    throw new CliUsageException(
                        $"Unknown option '{arg}' for 'index delta-spectre-console-cli'.",
                        HelpTopic.IndexDeltaSpectreConsoleCli,
                        options.Json);
            }
        }

        return options;
    }

    private static string ReadValue(string[] args, ref int index, string argName, bool json)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException(
                $"Expected a value after '{argName}'.",
                HelpTopic.IndexDeltaSpectreConsoleCli,
                json);
        }

        index++;
        return args[index];
    }

    private static int ReadPositiveInt(string[] args, ref int index, string argName, bool json)
    {
        var value = ReadValue(args, ref index, argName, json);
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new CliUsageException(
                $"Expected a positive integer after '{argName}'.",
                HelpTopic.IndexDeltaSpectreConsoleCli,
                json);
    }
}
