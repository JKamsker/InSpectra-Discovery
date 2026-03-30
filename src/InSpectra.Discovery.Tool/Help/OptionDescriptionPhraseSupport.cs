namespace InSpectra.Discovery.Tool.Help;

internal static class OptionDescriptionPhraseSupport
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
        "comma separated",
        "must be one of",
        "valid values",
        "output path",
        "input path",
    ];

    private static readonly string[] StrongValueHintPrefixes =
    [
        "Specify ",
        "Input ",
        "Name of ",
    ];

    private static readonly string[] IllustrativeValueExampleContains =
    [
        "something like",
        "specified .net runtime (",
        "specified .net runtime ",
    ];

    private static readonly string[] DescriptiveOverrideContains =
    [
        "fully qualified names",
        "separate by",
        "separated by",
        "use to set the version",
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
        => ContainsAny(description, IllustrativeValueExampleContains);

    public static bool AllowsDescriptiveValueEvidenceToOverrideFlag(string description)
        => ContainsAny(description, DescriptiveOverrideContains);

    private static bool StartsWithAny(string value, IReadOnlyList<string> prefixes)
        => prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string value, IReadOnlyList<string> fragments)
        => fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}

