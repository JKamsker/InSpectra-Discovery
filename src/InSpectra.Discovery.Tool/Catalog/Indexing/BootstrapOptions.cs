namespace InSpectra.Discovery.Tool.Catalog.Indexing;

internal sealed class BootstrapOptions
{
    public const string DefaultOutputPath = "artifacts/index/dotnet-tools.current.json";
    public const string DefaultPrefixAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    public const string DefaultServiceIndexUrl = "https://api.nuget.org/v3/index.json";

    public bool Json { get; init; }
    public string OutputPath { get; init; } = DefaultOutputPath;
    public string PrefixAlphabet { get; init; } = DefaultPrefixAlphabet;
    public string ServiceIndexUrl { get; init; } = DefaultServiceIndexUrl;
    public int PageSize { get; init; } = 1000;
    public int MetadataConcurrency { get; init; } = 12;
}

