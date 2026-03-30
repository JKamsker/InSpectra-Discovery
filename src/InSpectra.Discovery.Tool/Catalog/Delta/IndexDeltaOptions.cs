namespace InSpectra.Discovery.Tool.Catalog.Delta;

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
}

