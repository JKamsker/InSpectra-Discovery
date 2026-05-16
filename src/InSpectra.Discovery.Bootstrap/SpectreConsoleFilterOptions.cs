internal sealed class SpectreConsoleFilterOptions
{
    public const string DefaultInputPath = "artifacts/index/dotnet-tools.current.json";
    public const string DefaultOutputPath = "artifacts/index/dotnet-tools.spectre-console.json";

    public bool Json { get; init; }
    public string InputPath { get; init; } = DefaultInputPath;
    public string OutputPath { get; init; } = DefaultOutputPath;
    public int Concurrency { get; init; } = 16;

    public static SpectreConsoleFilterOptions Parse(string[] args)
    {
        var options = new SpectreConsoleFilterOptions();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            switch (arg)
            {
                case "--json":
                    options = options.WithJson();
                    break;
                case "--input":
                    options = options.WithInputPath(ReadValue(args, ref index, arg));
                    break;
                case "--output":
                    options = options.WithOutputPath(ReadValue(args, ref index, arg));
                    break;
                case "--concurrency":
                    options = options.WithConcurrency(ReadPositiveInt(args, ref index, arg));
                    break;
                default:
                    throw new CliUsageException(
                        $"Unknown option '{arg}' for 'filter spectre-console'.",
                        HelpTopic.FilterSpectreConsole,
                        options.Json);
            }
        }

        return options;
    }

    private SpectreConsoleFilterOptions WithConcurrency(int value) => new()
    {
        Json = Json,
        InputPath = InputPath,
        OutputPath = OutputPath,
        Concurrency = value,
    };

    private SpectreConsoleFilterOptions WithInputPath(string value) => new()
    {
        Json = Json,
        InputPath = value,
        OutputPath = OutputPath,
        Concurrency = Concurrency,
    };

    private SpectreConsoleFilterOptions WithOutputPath(string value) => new()
    {
        Json = Json,
        InputPath = InputPath,
        OutputPath = value,
        Concurrency = Concurrency,
    };

    private SpectreConsoleFilterOptions WithJson() => new()
    {
        Json = true,
        InputPath = InputPath,
        OutputPath = OutputPath,
        Concurrency = Concurrency,
    };

    private static int ReadPositiveInt(string[] args, ref int index, string argName)
    {
        var value = ReadValue(args, ref index, argName);
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new CliUsageException(
                $"Expected a positive integer after '{argName}'.",
                HelpTopic.FilterSpectreConsole,
                ContainsJson(args));
    }

    private static string ReadValue(string[] args, ref int index, string argName)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException(
                $"Expected a value after '{argName}'.",
                HelpTopic.FilterSpectreConsole,
                ContainsJson(args));
        }

        index++;
        return args[index];
    }

    private static bool ContainsJson(IEnumerable<string> args)
        => args.Any(arg => string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase));
}
