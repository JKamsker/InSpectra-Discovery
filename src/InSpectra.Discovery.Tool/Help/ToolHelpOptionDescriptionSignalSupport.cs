namespace InSpectra.Discovery.Tool.Help;

internal static class ToolHelpOptionDescriptionSignalSupport
{
    public static bool IsInformationalOptionDescription(string description)
        => ToolHelpOptionDescriptionPhraseSupport.IsInformationalOptionDescription(description);

    public static bool LooksLikeFlagDescription(string description)
        => ToolHelpOptionDescriptionPhraseSupport.LooksLikeFlagDescription(description);

    public static bool ContainsStrongValueDescriptionHint(string description)
        => ToolHelpOptionDescriptionPhraseSupport.ContainsStrongValueDescriptionHint(description);

    public static bool ContainsIllustrativeValueExample(string description)
        => ToolHelpOptionDescriptionPhraseSupport.ContainsIllustrativeValueExample(description);

    public static bool AllowsDescriptiveValueEvidenceToOverrideFlag(string description)
        => ToolHelpOptionDescriptionPhraseSupport.AllowsDescriptiveValueEvidenceToOverrideFlag(description);

    public static bool ContainsInlineOptionExample(ToolHelpOptionSignature signature, string description)
        => ToolHelpInlineOptionExampleSupport.ContainsInlineOptionExample(signature, description);
}

