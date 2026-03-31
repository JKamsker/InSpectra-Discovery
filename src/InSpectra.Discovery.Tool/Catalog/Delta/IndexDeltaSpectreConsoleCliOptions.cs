namespace InSpectra.Discovery.Tool.Catalog.Delta;


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
}

