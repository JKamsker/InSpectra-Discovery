using System.Text.RegularExpressions;

internal static partial class OpenCliDocumentTitleCleaner
{
    public static string? CleanTitle(string title)
    {
        var trimmed = title.Trim();

        trimmed = CopyrightPrefixRegex().Replace(trimmed, string.Empty).Trim();
        trimmed = InlineCopyrightRegex().Replace(trimmed, " ").Trim();
        trimmed = TrailingVersionNoiseRegex().Replace(trimmed, string.Empty).Trim();
        trimmed = trimmed.Trim('(', ')').Trim();

        if (BareVersionRegex().IsMatch(trimmed))
        {
            return null;
        }

        trimmed = TrailingDashRegex().Replace(trimmed, string.Empty).Trim();
        trimmed = MultipleSpacesRegex().Replace(trimmed, " ").Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    [GeneratedRegex(@"^copyright\s+(?:\(c\)\s+)?(?:\d{4}\s+)?(?:\(c\)\s+)?", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CopyrightPrefixRegex();

    [GeneratedRegex(@"\s*\(c\)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex InlineCopyrightRegex();

    [GeneratedRegex(@"\s+(?:version[:\s]*|v)\d+[\d.+\-a-z]*(?:\s+.*)?$|\s*\[dotnet\s+SDK\s+[^\]]+\]$|\s*for\s+\.NET\s+Core$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TrailingVersionNoiseRegex();

    [GeneratedRegex(@"^\d+\.\d+[\d.+\-a-zA-Z]*$", RegexOptions.Compiled)]
    private static partial Regex BareVersionRegex();

    [GeneratedRegex(@"\s+--?\s*$", RegexOptions.Compiled)]
    private static partial Regex TrailingDashRegex();

    [GeneratedRegex(@"\s{2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleSpacesRegex();
}
