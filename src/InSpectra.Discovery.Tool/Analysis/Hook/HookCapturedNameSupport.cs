namespace InSpectra.Discovery.Tool.Analysis.Hook;

using InSpectra.Discovery.Tool.Help.Signatures;
using InSpectra.Discovery.Tool.OpenCli.Structure;

internal static class HookCapturedNameSupport
{
    public static string? ResolveOptionArgumentName(HookCapturedOption option)
    {
        var rawName = option.ArgumentName?.Trim();
        if (OpenCliNameValidationSupport.IsPublishableArgumentName(rawName))
        {
            return OptionSignatureSupport.NormalizeArgumentName(rawName!);
        }

        var inferredName = OptionSignatureSupport.InferArgumentNameFromOption(option.Name);
        if (OpenCliNameValidationSupport.IsPublishableArgumentName(inferredName))
        {
            return inferredName;
        }

        return null;
    }

    public static string ResolvePositionalArgumentName(HookCapturedArgument argument, int index)
    {
        var rawName = argument.Name?.Trim();
        if (OpenCliNameValidationSupport.IsPublishableArgumentName(rawName))
        {
            return OptionSignatureSupport.NormalizeArgumentName(rawName!);
        }

        return index == 0 ? "VALUE" : $"VALUE_{index + 1}";
    }
}
