internal enum SpectreConsoleFilterMode
{
    AnySpectreConsole,
    SpectreConsoleCliOnly,
}

internal sealed class SpectreConsoleFilterOptions
{
    public const string DefaultInputPath = "artifacts/index/dotnet-tools.current.json";
    public const string DefaultSpectreConsoleOutputPath = "artifacts/index/dotnet-tools.spectre-console.json";
    public const string DefaultSpectreConsoleCliOutputPath = "artifacts/index/dotnet-tools.spectre-console-cli.json";

    public bool Json { get; init; }
    public SpectreConsoleFilterMode Mode { get; init; } = SpectreConsoleFilterMode.AnySpectreConsole;
    public string InputPath { get; init; } = DefaultInputPath;
    public string OutputPath { get; init; } = DefaultSpectreConsoleOutputPath;
    public int Concurrency { get; init; } = 16;

    public string CommandName => Mode switch
    {
        SpectreConsoleFilterMode.AnySpectreConsole => "filter spectre-console",
        SpectreConsoleFilterMode.SpectreConsoleCliOnly => "filter spectre-console-cli",
        _ => throw new ArgumentOutOfRangeException(nameof(Mode), Mode, null),
    };

    public string FilterName => Mode switch
    {
        SpectreConsoleFilterMode.AnySpectreConsole => "spectre-console",
        SpectreConsoleFilterMode.SpectreConsoleCliOnly => "spectre-console-cli",
        _ => throw new ArgumentOutOfRangeException(nameof(Mode), Mode, null),
    };

    public string EvidenceLabel => Mode switch
    {
        SpectreConsoleFilterMode.AnySpectreConsole => "Spectre.Console or Spectre.Console.Cli",
        SpectreConsoleFilterMode.SpectreConsoleCliOnly => "Spectre.Console.Cli",
        _ => throw new ArgumentOutOfRangeException(nameof(Mode), Mode, null),
    };

    public static SpectreConsoleFilterOptions Parse(string[] args, SpectreConsoleFilterMode mode)
    {
        var options = new SpectreConsoleFilterOptions
        {
            Mode = mode,
            OutputPath = GetDefaultOutputPath(mode),
        };

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            switch (arg)
            {
                case "--json":
                    options = options.WithJson();
                    break;
                case "--input":
                    options = options.WithInputPath(ReadValue(args, ref index, arg, mode));
                    break;
                case "--output":
                    options = options.WithOutputPath(ReadValue(args, ref index, arg, mode));
                    break;
                case "--concurrency":
                    options = options.WithConcurrency(ReadPositiveInt(args, ref index, arg, mode));
                    break;
                default:
                    throw new CliUsageException(
                        $"Unknown option '{arg}' for '{options.CommandName}'.",
                        GetHelpTopic(mode),
                        options.Json);
            }
        }

        return options;
    }

    private SpectreConsoleFilterOptions WithConcurrency(int value) => new()
    {
        Json = Json,
        Mode = Mode,
        InputPath = InputPath,
        OutputPath = OutputPath,
        Concurrency = value,
    };

    private SpectreConsoleFilterOptions WithInputPath(string value) => new()
    {
        Json = Json,
        Mode = Mode,
        InputPath = value,
        OutputPath = OutputPath,
        Concurrency = Concurrency,
    };

    private SpectreConsoleFilterOptions WithOutputPath(string value) => new()
    {
        Json = Json,
        Mode = Mode,
        InputPath = InputPath,
        OutputPath = value,
        Concurrency = Concurrency,
    };

    private SpectreConsoleFilterOptions WithJson() => new()
    {
        Json = true,
        Mode = Mode,
        InputPath = InputPath,
        OutputPath = OutputPath,
        Concurrency = Concurrency,
    };

    private static int ReadPositiveInt(string[] args, ref int index, string argName, SpectreConsoleFilterMode mode)
    {
        var value = ReadValue(args, ref index, argName, mode);
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new CliUsageException(
                $"Expected a positive integer after '{argName}'.",
                GetHelpTopic(mode),
                ContainsJson(args));
    }

    private static string ReadValue(string[] args, ref int index, string argName, SpectreConsoleFilterMode mode)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException(
                $"Expected a value after '{argName}'.",
                GetHelpTopic(mode),
                ContainsJson(args));
        }

        index++;
        return args[index];
    }

    private static string GetDefaultOutputPath(SpectreConsoleFilterMode mode)
        => mode switch
        {
            SpectreConsoleFilterMode.AnySpectreConsole => DefaultSpectreConsoleOutputPath,
            SpectreConsoleFilterMode.SpectreConsoleCliOnly => DefaultSpectreConsoleCliOutputPath,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

    private static HelpTopic GetHelpTopic(SpectreConsoleFilterMode mode)
        => mode switch
        {
            SpectreConsoleFilterMode.AnySpectreConsole => HelpTopic.FilterSpectreConsole,
            SpectreConsoleFilterMode.SpectreConsoleCliOnly => HelpTopic.FilterSpectreConsoleCli,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

    private static bool ContainsJson(IEnumerable<string> args)
        => args.Any(arg => string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase));
}
