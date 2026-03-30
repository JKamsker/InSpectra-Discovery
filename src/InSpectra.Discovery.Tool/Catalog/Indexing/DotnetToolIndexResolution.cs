internal sealed record DotnetToolIndexResolution(
    DotnetToolIndexEntry? Entry,
    bool IsSkipped,
    string? SkipReason)
{
    public static DotnetToolIndexResolution Resolved(DotnetToolIndexEntry? entry)
        => new(entry, IsSkipped: false, SkipReason: null);

    public static DotnetToolIndexResolution Skip(string skipReason)
        => new(Entry: null, IsSkipped: true, SkipReason: skipReason);
}
