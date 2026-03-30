namespace InSpectra.Discovery.Tool.Help;

internal static class OptionDescriptionSignalSupport
{
    public static bool IsInformationalOptionDescription(string description)
        => OptionDescriptionPhraseSupport.IsInformationalOptionDescription(description);

    public static bool LooksLikeFlagDescription(string description)
        => OptionDescriptionPhraseSupport.LooksLikeFlagDescription(description);

    public static bool ContainsStrongValueDescriptionHint(string description)
        => OptionDescriptionPhraseSupport.ContainsStrongValueDescriptionHint(description);

    public static bool ContainsIllustrativeValueExample(string description)
        => OptionDescriptionPhraseSupport.ContainsIllustrativeValueExample(description);

    public static bool AllowsDescriptiveValueEvidenceToOverrideFlag(string description)
        => OptionDescriptionPhraseSupport.AllowsDescriptiveValueEvidenceToOverrideFlag(description);

    public static bool ContainsInlineOptionExample(OptionSignature signature, string description)
        => InlineOptionExampleSupport.ContainsInlineOptionExample(signature, description);
}

