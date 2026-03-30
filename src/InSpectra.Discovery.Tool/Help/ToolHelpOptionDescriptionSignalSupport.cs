internal static class ToolHelpOptionDescriptionSignalSupport
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

    private static readonly HashSet<string> InlineReferenceWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "is",
        "was",
        "are",
        "used",
        "specified",
        "set",
        "to",
        "for",
        "if",
        "when",
    };

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

    public static bool ContainsInlineOptionExample(ToolHelpOptionSignature signature, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        foreach (var optionToken in ToolHelpOptionSignatureSupport.EnumerateTokens(signature))
        {
            var searchIndex = 0;
            while (searchIndex < description.Length)
            {
                var matchIndex = description.IndexOf(optionToken, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    break;
                }

                if (!HasInlineOptionExampleBoundary(description, matchIndex, optionToken.Length))
                {
                    searchIndex = matchIndex + optionToken.Length;
                    continue;
                }

                var valueStart = matchIndex + optionToken.Length;
                while (valueStart < description.Length && char.IsWhiteSpace(description, valueStart))
                {
                    valueStart++;
                }

                if (valueStart < description.Length)
                {
                    var next = description[valueStart];
                    if (!char.IsWhiteSpace(next)
                        && next is not '-' and not '/' and not '.' and not ',' and not ';' and not ')')
                    {
                        if (!LooksLikeInlineReferenceWord(ReadInlineReferenceWord(description, valueStart)))
                        {
                            return true;
                        }
                    }
                }

                searchIndex = matchIndex + optionToken.Length;
            }
        }

        return false;
    }

    private static bool StartsWithAny(string value, IReadOnlyList<string> prefixes)
        => prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string value, IReadOnlyList<string> fragments)
        => fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool HasInlineOptionExampleBoundary(string description, int matchIndex, int tokenLength)
    {
        if (matchIndex > 0 && char.IsLetterOrDigit(description[matchIndex - 1]))
        {
            return false;
        }

        var endIndex = matchIndex + tokenLength;
        return endIndex >= description.Length || !char.IsLetterOrDigit(description[endIndex]);
    }

    private static string ReadInlineReferenceWord(string description, int startIndex)
    {
        var endIndex = startIndex;
        while (endIndex < description.Length)
        {
            var character = description[endIndex];
            if (char.IsWhiteSpace(character) || character is ',' or ';' or ')' or '(' or '[' or ']')
            {
                break;
            }

            endIndex++;
        }

        return description[startIndex..endIndex];
    }

    private static bool LooksLikeInlineReferenceWord(string word)
        => InlineReferenceWords.Contains(word);
}
