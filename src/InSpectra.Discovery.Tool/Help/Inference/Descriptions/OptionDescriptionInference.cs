namespace InSpectra.Discovery.Tool.Help.Inference.Descriptions;

using InSpectra.Discovery.Tool.Help.Signatures;

internal static class OptionDescriptionInference
{
    public static string? InferArgumentName(OptionSignature signature, string? description)
    {
        var primaryOption = signature.PrimaryName;
        if (string.IsNullOrWhiteSpace(primaryOption))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return OptionSignatureSupport.HasValueLikeOptionName(primaryOption)
                ? OptionSignatureSupport.InferArgumentNameFromOption(primaryOption)
                : null;
        }

        var normalizedDescription = NormalizeDescriptionForInference(description);
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return OptionSignatureSupport.HasValueLikeOptionName(primaryOption)
                ? OptionSignatureSupport.InferArgumentNameFromOption(primaryOption)
                : null;
        }

        var trimmedDescription = RequiredDescriptionSupport.TrimLeadingRequiredPrefix(normalizedDescription) ?? normalizedDescription;
        var descriptionWithoutDefault = TrimLeadingDefaultClause(trimmedDescription);
        var hasNonBooleanDefault = HasNonBooleanDefault(trimmedDescription);
        var defaultValue = GetDefaultValue(trimmedDescription);
        var descriptionForSignals = NormalizeDescriptionForSignals(string.IsNullOrWhiteSpace(descriptionWithoutDefault)
            ? trimmedDescription
            : descriptionWithoutDefault);
        var hasRequiredPrefix = RequiredDescriptionSupport.StartsWithRequiredPrefix(normalizedDescription);
        var hasInlineOptionExample = OptionDescriptionSignalSupport.ContainsInlineOptionExample(signature, normalizedDescription);
        var hasExplicitValueEvidence = hasNonBooleanDefault
            || hasInlineOptionExample
            || OptionDescriptionSignalSupport.ContainsIllustrativeValueExample(descriptionForSignals);
        var hasDescriptiveValueEvidence = OptionDescriptionSignalSupport.ContainsStrongValueDescriptionHint(descriptionForSignals);
        if (string.IsNullOrWhiteSpace(trimmedDescription))
        {
            return hasRequiredPrefix && OptionSignatureSupport.HasValueLikeOptionName(primaryOption)
                ? OptionSignatureSupport.InferArgumentNameFromOption(primaryOption)
                : null;
        }

        if (IsBooleanDefaultValue(defaultValue) && !(hasExplicitValueEvidence || hasDescriptiveValueEvidence))
        {
            return null;
        }

        if (OptionDescriptionSignalSupport.IsInformationalOptionDescription(trimmedDescription))
        {
            return null;
        }

        if (OptionDescriptionSignalSupport.LooksLikeFlagDescription(descriptionForSignals))
        {
            var descriptiveOverride = hasDescriptiveValueEvidence
                && OptionDescriptionSignalSupport.AllowsDescriptiveValueEvidenceToOverrideFlag(descriptionForSignals);
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
            || OptionSignatureSupport.HasValueLikeOptionName(primaryOption)
                ? OptionSignatureSupport.InferArgumentNameFromOption(primaryOption)
                : null;
    }

    public static bool HasNonBooleanDefault(string description)
    {
        var defaultValue = GetDefaultValue(description);
        return !string.IsNullOrWhiteSpace(defaultValue)
            && !IsBooleanDefaultValue(defaultValue);
    }

    public static bool StartsWithRequiredPrefix(string? description)
        => RequiredDescriptionSupport.StartsWithRequiredPrefix(description);

    public static string? TrimLeadingRequiredPrefix(string? description)
        => RequiredDescriptionSupport.TrimLeadingRequiredPrefix(description);

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

