namespace InSpectra.Discovery.Tool.Help;

internal static class ToolHelpUsageArgumentSelectionSupport
{
    public static IReadOnlyList<ToolHelpItem> Select(
        IReadOnlyList<ToolHelpItem> explicitArguments,
        IReadOnlyList<ToolHelpItem> usageArguments)
    {
        if (explicitArguments.Count == 0)
        {
            return usageArguments;
        }

        if (usageArguments.Count == 0)
        {
            return explicitArguments.All(ToolHelpArgumentNodeBuilder.IsLowSignalExplicitArgument)
                ? []
                : explicitArguments;
        }

        if (explicitArguments.Count != usageArguments.Count)
        {
            return explicitArguments.All(ToolHelpArgumentNodeBuilder.IsLowSignalExplicitArgument)
                ? usageArguments
                : explicitArguments;
        }

        var merged = new List<ToolHelpItem>(explicitArguments.Count);
        var changed = false;
        for (var index = 0; index < explicitArguments.Count; index++)
        {
            var mergedArgument = Merge(explicitArguments[index], usageArguments[index]);
            merged.Add(mergedArgument);
            changed |= !string.Equals(explicitArguments[index].Key, mergedArgument.Key, StringComparison.Ordinal)
                || explicitArguments[index].IsRequired != mergedArgument.IsRequired
                || !string.Equals(explicitArguments[index].Description, mergedArgument.Description, StringComparison.Ordinal);
        }

        return changed ? merged : explicitArguments;
    }

    private static ToolHelpItem Merge(ToolHelpItem explicitArgument, ToolHelpItem usageArgument)
    {
        if (!ToolHelpArgumentNodeBuilder.TryParseArgumentSignature(explicitArgument.Key, out var explicitSignature)
            || !ToolHelpArgumentNodeBuilder.TryParseArgumentSignature(usageArgument.Key, out var usageSignature))
        {
            return explicitArgument;
        }

        return explicitSignature.Name == usageSignature.Name
            ? new ToolHelpItem(
                usageArgument.Key,
                explicitArgument.IsRequired || usageArgument.IsRequired,
                explicitArgument.Description ?? usageArgument.Description)
            : explicitArgument;
    }
}

