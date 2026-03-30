namespace InSpectra.Discovery.Tool.Indexing;

using System.Text.Json.Nodes;

internal static class RepositoryPackageIndexTimestampSupport
{
    public static string? ToIsoTimestamp(JsonNode? value)
    {
        var text = value?.GetValue<string>();
        return string.IsNullOrWhiteSpace(text) ? null : ParseDateTimeOrMin(text).ToUniversalTime().ToString("O");
    }

    public static PackageEntryTimestamps ResolvePackageTimestamps(JsonObject package)
    {
        var timestamps = package["versions"]?.AsArray().OfType<JsonObject>()
            .Select(ResolveVersionTimestamp)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToList()
            ?? [];

        if (timestamps.Count == 0)
        {
            return new PackageEntryTimestamps(null, null);
        }

        return new PackageEntryTimestamps(
            timestamps[0].ToString("O"),
            timestamps[^1].ToString("O"));
    }

    public static DocumentTimestamps ResolveDocumentTimestamps(string path, DateTimeOffset now)
    {
        var existing = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            : null;
        var createdAt = ToIsoTimestamp(existing?["createdAt"])
            ?? ToIsoTimestamp(existing?["generatedAt"])
            ?? now.ToString("O");

        return new DocumentTimestamps(createdAt, now.ToString("O"));
    }

    public static DateTimeOffset ParseDateTimeOrMin(string? value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;

    private static DateTimeOffset? ResolveVersionTimestamp(JsonObject version)
    {
        if (TryParseMeaningfulDateTime(version["publishedAt"]?.GetValue<string>(), out var publishedAt))
        {
            return publishedAt;
        }

        if (TryParseMeaningfulDateTime(version["evaluatedAt"]?.GetValue<string>(), out var evaluatedAt))
        {
            return evaluatedAt;
        }

        return null;
    }

    private static bool TryParseDateTime(string? value, out DateTimeOffset parsed)
    {
        if (DateTimeOffset.TryParse(value, out parsed))
        {
            parsed = parsed.ToUniversalTime();
            return true;
        }

        return false;
    }

    private static bool TryParseMeaningfulDateTime(string? value, out DateTimeOffset parsed)
    {
        if (TryParseDateTime(value, out parsed) && parsed.Year > 1900)
        {
            return true;
        }

        parsed = default;
        return false;
    }
}

internal sealed record PackageEntryTimestamps(string? CreatedAt, string? UpdatedAt);

internal sealed record DocumentTimestamps(string CreatedAt, string UpdatedAt);

