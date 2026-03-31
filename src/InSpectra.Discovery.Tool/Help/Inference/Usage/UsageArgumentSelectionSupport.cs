namespace InSpectra.Discovery.Tool.Help.Inference.Usage;

using InSpectra.Discovery.Tool.Help.OpenCli;

using InSpectra.Discovery.Tool.Help.Documents;

internal static class UsageArgumentSelectionSupport
{
    public static IReadOnlyList<Item> Select(
        IReadOnlyList<Item> explicitArguments,
        IReadOnlyList<Item> usageArguments)
    {
        if (explicitArguments.Count == 0)
        {
            return usageArguments;
        }

        if (usageArguments.Count == 0)
        {
            return explicitArguments.All(ArgumentNodeBuilder.IsLowSignalExplicitArgument)
                ? []
                : explicitArguments;
        }

        if (explicitArguments.Count != usageArguments.Count)
        {
            return explicitArguments.All(ArgumentNodeBuilder.IsLowSignalExplicitArgument)
                ? usageArguments
                : explicitArguments;
        }

        var merged = new List<Item>(explicitArguments.Count);
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

    private static Item Merge(Item explicitArgument, Item usageArgument)
    {
        if (!ArgumentNodeBuilder.TryParseArgumentSignature(explicitArgument.Key, out var explicitSignature)
            || !ArgumentNodeBuilder.TryParseArgumentSignature(usageArgument.Key, out var usageSignature))
        {
            return explicitArgument;
        }

        return explicitSignature.Name == usageSignature.Name
            ? new Item(
                usageArgument.Key,
                explicitArgument.IsRequired || usageArgument.IsRequired,
                explicitArgument.Description ?? usageArgument.Description)
            : explicitArgument;
    }
}

