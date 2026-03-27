internal sealed class CliFxFilterOptions
{
    public const string DefaultInputPath = "artifacts/index/dotnet-tools.current.json";
    public const string DefaultOutputPath = "artifacts/index/dotnet-tools.clifx.json";

    public bool Json { get; init; }
    public string InputPath { get; init; } = DefaultInputPath;
    public string OutputPath { get; init; } = DefaultOutputPath;
    public int Concurrency { get; init; } = 16;

    public const string CommandName = "catalog filter clifx";
    public const string FilterName = "clifx";
    public const string EvidenceLabel = "CliFx";
}
