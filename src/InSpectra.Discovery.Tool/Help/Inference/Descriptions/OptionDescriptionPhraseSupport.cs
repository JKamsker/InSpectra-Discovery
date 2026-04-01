namespace InSpectra.Discovery.Tool.Help.Inference.Descriptions;

using System.Text.RegularExpressions;

internal static partial class OptionDescriptionPhraseSupport
{
    private static readonly HashSet<string> InformationalOptionDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Display this help screen.",
        "Display version information.",
        "Show help information.",
        "Show help and usage information",
    };

    private static readonly string[] InformationalPrefixes =
    [
        "Display version information",
        "Display the program version",
        "Display this help",
        "Show version information",
        "Show help",
    ];

    private static readonly string[] FlagDescriptionPrefixes =
    [
        "Actually ",
        "Allow ",
        "Append ",
        "Check ",
        "Continue ",
        "Convert ",
        "Create ",
        "Creates ",
        "Delete ",
        "Determine ",
        "Disable ",
        "Display ",
        "Don't ",
        "Enable ",
        "Enables ",
        "Escape ",
        "Exit ",
        "Flatten ",
        "Force ",
        "Gather ",
        "Generate ",
        "Generates ",
        "Hashes ",
        "If--",
        "If --",
        "Include ",
        "Merge ",
        "Merges ",
        "Minify ",
        "Overwrite ",
        "Pack ",
        "Packs ",
        "Preserve ",
        "Pretty ",
        "Print ",
        "Report ",
        "Recursively ",
        "Run ",
        "Save ",
        "Set true to ",
        "Set false to ",
        "Show ",
        "Skip ",
        "Skips ",
        "Sort ",
        "Suppress ",
        "Toggle ",
        "Update ",
        "Use ",
        "Verbose ",
        "Write ",
        "Wrap ",
        "Whether ",
    ];

    private static readonly string[] StrongValueHintContains =
    [
        "path to",
        "file path",
        "file name",
        "directory",
        "connection string",
        "package ids",
        "definition string",
        "reference string",
        "parameters for",
        "comma separated",
        "comma-separated",
        "must be one of",
        "supported values",
        "acceptable values",
        "valid values",
        "output path",
        "input path",
        "queue name",
        "friendly name",
        "sort key",
        "utc datetime",
        "folder names",
        "start folder",
        "url extension",
        "use to set the version",
        "header to write",
        "replace string",
        "game release",
        "days to keep",
        "properties to categorize",
        "fully qualified names",
        "separate by",
        "must be positive",
    ];

    private static readonly string[] StrongValueHintPrefixes =
    [
        "Specify ",
        "Input ",
        "Name of ",
        "Number of ",
    ];

    private static readonly string[] IllustrativeValueExampleContains =
    [
        "something like",
        "specified .net runtime (",
        "specified .net runtime ",
        "supported values:",
        "acceptable values are:",
    ];

    private static readonly string[] DescriptiveOverrideContains =
    [
        "fully qualified names",
        "separate by",
        "separated by",
        "use to set the version",
        "replace string",
    ];

    public static bool IsInformationalOptionDescription(string description)
        => InformationalOptionDescriptions.Contains(description)
            || StartsWithAny(description, InformationalPrefixes);

    public static bool LooksLikeFlagDescription(string description)
        => (description.StartsWith("List ", StringComparison.OrdinalIgnoreCase)
                && !description.StartsWith("List of ", StringComparison.OrdinalIgnoreCase))
            || StartsWithAny(description, FlagDescriptionPrefixes);

    public static bool ContainsStrongValueDescriptionHint(string description)
        => ContainsAny(description, StrongValueHintContains)
            || StartsWithAny(description, StrongValueHintPrefixes);

    public static bool ContainsIllustrativeValueExample(string description)
        => ContainsAny(description, IllustrativeValueExampleContains)
            || ParenthesizedAlternationRegex().IsMatch(description);

    public static bool AllowsDescriptiveValueEvidenceToOverrideFlag(string description)
        => ContainsAny(description, DescriptiveOverrideContains);

    private static bool StartsWithAny(string value, IReadOnlyList<string> prefixes)
        => prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string value, IReadOnlyList<string> fragments)
        => fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"\([^)]*[^)\s|][^)]*\|[^)]*[^)\s|][^)]*\)", RegexOptions.Compiled)]
    private static partial Regex ParenthesizedAlternationRegex();
}
