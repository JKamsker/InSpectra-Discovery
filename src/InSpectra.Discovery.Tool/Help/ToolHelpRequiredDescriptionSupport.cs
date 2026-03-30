internal static class ToolHelpRequiredDescriptionSupport
{
    private static readonly string[] RequiredPrefixes =
    [
        "Required.",
        "Required ",
        "(REQUIRED)",
        "[REQUIRED]",
    ];

    public static bool StartsWithRequiredPrefix(string? description)
        => description is { Length: > 0 }
            && TryTrimLeadingRequiredPrefix(description, out _);

    public static string? TrimLeadingRequiredPrefix(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        return TryTrimLeadingRequiredPrefix(description, out var trimmed)
            ? trimmed
            : description;
    }

    private static bool TryTrimLeadingRequiredPrefix(string description, out string trimmed)
    {
        var normalized = description.TrimStart();
        foreach (var prefix in RequiredPrefixes)
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            trimmed = normalized[prefix.Length..].TrimStart();
            return true;
        }

        trimmed = description;
        return false;
    }
}
