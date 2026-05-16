namespace InSpectra.Discovery.Tool.Catalog.Delta;


internal sealed record IndexDeltaAllToolsOptions
{
    public const string DefaultInputDeltaPath = "state/discovery/dotnet-tools.delta.json";
    public const string DefaultOutputDeltaPath = "state/discovery/dotnet-tools.all-tools.delta.json";
    public const string DefaultQueueOutputPath = "state/discovery/dotnet-tools.all-tools.queue.json";

    public bool Json { get; init; }
    public string InputDeltaPath { get; init; } = DefaultInputDeltaPath;
    public string OutputDeltaPath { get; init; } = DefaultOutputDeltaPath;
    public string QueueOutputPath { get; init; } = DefaultQueueOutputPath;
}

