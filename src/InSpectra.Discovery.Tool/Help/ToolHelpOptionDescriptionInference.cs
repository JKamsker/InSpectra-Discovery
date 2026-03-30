internal static class ToolHelpOptionDescriptionInference
{
    private static readonly HashSet<string> InformationalOptionDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Display this help screen.",
        "Display version information.",
        "Show help information.",
        "Show help and usage information",
    };

    public static string? InferArgumentName(ToolHelpOptionSignature signature, string? description)
    {
        var primaryOption = signature.PrimaryName;
        if (string.IsNullOrWhiteSpace(primaryOption))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return ToolHelpOptionSignatureSupport.HasValueLikeOptionName(primaryOption)
                ? ToolHelpOptionSignatureSupport.InferArgumentNameFromOption(primaryOption)
                : null;
        }

        var normalizedDescription = NormalizeDescriptionForInference(description);
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return ToolHelpOptionSignatureSupport.HasValueLikeOptionName(primaryOption)
                ? ToolHelpOptionSignatureSupport.InferArgumentNameFromOption(primaryOption)
                : null;
        }

        var trimmedDescription = TrimLeadingRequiredPrefix(normalizedDescription) ?? normalizedDescription;
        var descriptionWithoutDefault = TrimLeadingDefaultClause(trimmedDescription);
        var hasNonBooleanDefault = HasNonBooleanDefault(trimmedDescription);
        var defaultValue = GetDefaultValue(trimmedDescription);
        var descriptionForSignals = NormalizeDescriptionForSignals(string.IsNullOrWhiteSpace(descriptionWithoutDefault)
            ? trimmedDescription
            : descriptionWithoutDefault);
        var hasRequiredPrefix = StartsWithRequiredPrefix(normalizedDescription);
        var hasInlineOptionExample = ContainsInlineOptionExample(signature, normalizedDescription);
        var hasExplicitValueEvidence = hasNonBooleanDefault
            || hasInlineOptionExample
            || ContainsIllustrativeValueExample(descriptionForSignals);
        var hasDescriptiveValueEvidence = ContainsStrongValueDescriptionHint(descriptionForSignals);
        if (string.IsNullOrWhiteSpace(trimmedDescription))
        {
            return hasRequiredPrefix && ToolHelpOptionSignatureSupport.HasValueLikeOptionName(primaryOption)
                ? ToolHelpOptionSignatureSupport.InferArgumentNameFromOption(primaryOption)
                : null;
        }

        if (IsBooleanDefaultValue(defaultValue) && !(hasExplicitValueEvidence || hasDescriptiveValueEvidence))
        {
            return null;
        }

        if (IsInformationalOptionDescription(trimmedDescription))
        {
            return null;
        }

        if (LooksLikeFlagDescription(descriptionForSignals))
        {
            var descriptiveOverride = hasDescriptiveValueEvidence
                && AllowsDescriptiveValueEvidenceToOverrideFlag(descriptionForSignals);
            var onlyDefaultBacksThis = hasNonBooleanDefault
                && !hasInlineOptionExample
                && !hasDescriptiveValueEvidence;
            if (onlyDefaultBacksThis || (!hasExplicitValueEvidence && !descriptiveOverride))
            {
                return null;
            }
        }

        return hasRequiredPrefix
            || hasExplicitValueEvidence
            || hasDescriptiveValueEvidence
            || ToolHelpOptionSignatureSupport.HasValueLikeOptionName(primaryOption)
                ? ToolHelpOptionSignatureSupport.InferArgumentNameFromOption(primaryOption)
                : null;
    }

    public static bool HasNonBooleanDefault(string description)
    {
        var defaultValue = GetDefaultValue(description);
        return !string.IsNullOrWhiteSpace(defaultValue)
            && !IsBooleanDefaultValue(defaultValue);
    }

    public static bool StartsWithRequiredPrefix(string? description)
        => !string.IsNullOrWhiteSpace(description)
            && (
                description.TrimStart().StartsWith("Required.", StringComparison.OrdinalIgnoreCase)
                || description.TrimStart().StartsWith("Required ", StringComparison.OrdinalIgnoreCase)
                || description.TrimStart().StartsWith("(REQUIRED)", StringComparison.OrdinalIgnoreCase)
                || description.TrimStart().StartsWith("[REQUIRED]", StringComparison.OrdinalIgnoreCase));

    public static string? TrimLeadingRequiredPrefix(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        var normalized = description.TrimStart();
        if (normalized.StartsWith("Required.", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["Required.".Length..].TrimStart();
        }

        if (normalized.StartsWith("Required ", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["Required ".Length..].TrimStart();
        }

        if (normalized.StartsWith("(REQUIRED)", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["(REQUIRED)".Length..].TrimStart();
        }

        if (normalized.StartsWith("[REQUIRED]", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["[REQUIRED]".Length..].TrimStart();
        }

        return description;
    }

    private static string NormalizeDescriptionForInference(string description)
        => string.Join(
            " ",
            description
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0))
            .Trim();

    private static string NormalizeDescriptionForSignals(string description)
    {
        var normalized = description.TrimStart();
        while (normalized.StartsWith("(", StringComparison.Ordinal))
        {
            var closingIndex = normalized.IndexOf(')');
            if (closingIndex < 0)
            {
                break;
            }

            normalized = normalized[(closingIndex + 1)..].TrimStart();
        }

        return normalized.StartsWith("Override:", StringComparison.OrdinalIgnoreCase)
            ? normalized["Override:".Length..].TrimStart()
            : normalized;
    }

    private static bool IsInformationalOptionDescription(string description)
        => InformationalOptionDescriptions.Contains(description)
            || description.StartsWith("Display version information", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Display the program version", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Display this help", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Show version information", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Show help", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeFlagDescription(string description)
        => description.StartsWith("Actually ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Allow ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Append ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Check ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Continue ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Convert ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Create ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Creates ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Delete ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Determine ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Disable ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Display ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Don't ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Enable ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Enables ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Escape ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Exit ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Flatten ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Force ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Gather ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Generate ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Generates ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Hashes ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("If--", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("If --", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Include ", StringComparison.OrdinalIgnoreCase)
            || (description.StartsWith("List ", StringComparison.OrdinalIgnoreCase)
                && !description.StartsWith("List of ", StringComparison.OrdinalIgnoreCase))
            || description.StartsWith("Merge ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Merges ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Minify ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Overwrite ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Pack ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Packs ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Preserve ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Pretty ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Print ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Report ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Run ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Save ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Set true to ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Set false to ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Show ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Skip ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Skips ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Sort ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Suppress ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Toggle ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Update ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Use ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Verbose ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Wrap ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Whether ", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsStrongValueDescriptionHint(string description)
        => description.Contains("path to", StringComparison.OrdinalIgnoreCase)
            || description.Contains("file path", StringComparison.OrdinalIgnoreCase)
            || description.Contains("file name", StringComparison.OrdinalIgnoreCase)
            || description.Contains("directory", StringComparison.OrdinalIgnoreCase)
            || description.Contains("connection string", StringComparison.OrdinalIgnoreCase)
            || description.Contains("package ids", StringComparison.OrdinalIgnoreCase)
            || description.Contains("comma separated", StringComparison.OrdinalIgnoreCase)
            || description.Contains("must be one of", StringComparison.OrdinalIgnoreCase)
            || description.Contains("valid values", StringComparison.OrdinalIgnoreCase)
            || description.Contains("output path", StringComparison.OrdinalIgnoreCase)
            || description.Contains("input path", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Specify ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Input ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Name of ", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsIllustrativeValueExample(string description)
        => description.Contains("something like", StringComparison.OrdinalIgnoreCase)
            || description.Contains("specified .net runtime (", StringComparison.OrdinalIgnoreCase)
            || description.Contains("specified .net runtime ", StringComparison.OrdinalIgnoreCase);

    private static bool AllowsDescriptiveValueEvidenceToOverrideFlag(string description)
        => description.Contains("fully qualified names", StringComparison.OrdinalIgnoreCase)
            || description.Contains("separate by", StringComparison.OrdinalIgnoreCase)
            || description.Contains("separated by", StringComparison.OrdinalIgnoreCase)
            || description.Contains("use to set the version", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsInlineOptionExample(ToolHelpOptionSignature signature, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var normalized = NormalizeDescriptionForInference(description);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        foreach (var optionToken in ToolHelpOptionSignatureSupport.EnumerateTokens(signature))
        {
            var searchIndex = 0;
            while (searchIndex < normalized.Length)
            {
                var matchIndex = normalized.IndexOf(optionToken, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    break;
                }

                if (!HasInlineOptionExampleBoundary(normalized, matchIndex, optionToken.Length))
                {
                    searchIndex = matchIndex + optionToken.Length;
                    continue;
                }

                var valueStart = matchIndex + optionToken.Length;
                while (valueStart < normalized.Length && char.IsWhiteSpace(normalized, valueStart))
                {
                    valueStart++;
                }

                if (valueStart < normalized.Length)
                {
                    var next = normalized[valueStart];
                    if (!char.IsWhiteSpace(next)
                        && next is not '-' and not '/' and not '.' and not ',' and not ';' and not ')')
                    {
                        if (!LooksLikeInlineReferenceWord(ReadInlineReferenceWord(normalized, valueStart)))
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
        => string.Equals(word, "is", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "was", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "are", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "used", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "specified", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "set", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "to", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "for", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "if", StringComparison.OrdinalIgnoreCase)
            || string.Equals(word, "when", StringComparison.OrdinalIgnoreCase);

    private static string? GetDefaultValue(string description)
    {
        var marker = "(Default:";
        var startIndex = description.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return null;
        }

        var valueStart = startIndex + marker.Length;
        var endIndex = description.IndexOf(')', valueStart);
        return endIndex <= valueStart
            ? null
            : description[valueStart..endIndex].Trim();
    }

    private static string TrimLeadingDefaultClause(string description)
    {
        var normalized = description.TrimStart();
        if (!normalized.StartsWith("(Default:", StringComparison.OrdinalIgnoreCase))
        {
            return description;
        }

        var endIndex = normalized.IndexOf(')');
        return endIndex < 0
            ? string.Empty
            : normalized[(endIndex + 1)..].TrimStart();
    }

    private static bool IsBooleanDefaultValue(string? defaultValue)
        => string.Equals(defaultValue, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(defaultValue, "true", StringComparison.OrdinalIgnoreCase);
}
