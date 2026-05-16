namespace InSpectra.Discovery.Tool.Catalog.Filtering.SpectreConsole;

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
}

