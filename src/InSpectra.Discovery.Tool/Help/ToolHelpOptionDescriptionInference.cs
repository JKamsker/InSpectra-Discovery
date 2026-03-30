internal static class ToolHelpOptionDescriptionInference
{
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

        var trimmedDescription = ToolHelpRequiredDescriptionSupport.TrimLeadingRequiredPrefix(normalizedDescription) ?? normalizedDescription;
        var descriptionWithoutDefault = TrimLeadingDefaultClause(trimmedDescription);
        var hasNonBooleanDefault = HasNonBooleanDefault(trimmedDescription);
        var defaultValue = GetDefaultValue(trimmedDescription);
        var descriptionForSignals = NormalizeDescriptionForSignals(string.IsNullOrWhiteSpace(descriptionWithoutDefault)
            ? trimmedDescription
            : descriptionWithoutDefault);
        var hasRequiredPrefix = ToolHelpRequiredDescriptionSupport.StartsWithRequiredPrefix(normalizedDescription);
        var hasInlineOptionExample = ToolHelpOptionDescriptionSignalSupport.ContainsInlineOptionExample(signature, normalizedDescription);
        var hasExplicitValueEvidence = hasNonBooleanDefault
            || hasInlineOptionExample
            || ToolHelpOptionDescriptionSignalSupport.ContainsIllustrativeValueExample(descriptionForSignals);
        var hasDescriptiveValueEvidence = ToolHelpOptionDescriptionSignalSupport.ContainsStrongValueDescriptionHint(descriptionForSignals);
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

        if (ToolHelpOptionDescriptionSignalSupport.IsInformationalOptionDescription(trimmedDescription))
        {
            return null;
        }

        if (ToolHelpOptionDescriptionSignalSupport.LooksLikeFlagDescription(descriptionForSignals))
        {
            var descriptiveOverride = hasDescriptiveValueEvidence
                && ToolHelpOptionDescriptionSignalSupport.AllowsDescriptiveValueEvidenceToOverrideFlag(descriptionForSignals);
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
        => ToolHelpRequiredDescriptionSupport.StartsWithRequiredPrefix(description);

    public static string? TrimLeadingRequiredPrefix(string? description)
        => ToolHelpRequiredDescriptionSupport.TrimLeadingRequiredPrefix(description);

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
