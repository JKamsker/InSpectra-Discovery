namespace InSpectra.Discovery.Tool.Help;

internal static class UsageArgumentExtractionSupport
{
    public static IReadOnlyList<Item> Extract(
        string commandName,
        string commandPath,
        IReadOnlyList<string> usageLines,
        bool hasChildCommands)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arguments = new List<Item>();
        var invocation = string.IsNullOrWhiteSpace(commandPath)
            ? commandName
            : $"{commandName} {commandPath}";
        string? previousNonEmptyLine = null;

        foreach (var line in usageLines)
        {
            var lineArguments = new List<Item>();
            var stopLine = false;
            foreach (var argument in BracketedUsageArgumentSupport.Extract(line, seen, hasChildCommands, out stopLine))
            {
                lineArguments.Add(argument);
                arguments.Add(argument);
            }

            if (stopLine)
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (lineArguments.Count > 0)
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (BareUsageArgumentSupport.LooksLikeWrappedUsageValueContinuation(previousNonEmptyLine, line))
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (!BareUsageArgumentSupport.LooksLikeExampleLabel(previousNonEmptyLine))
            {
                var bareArgument = BareUsageArgumentSupport.TryExtract(invocation, line, hasChildCommands);
                if (bareArgument is not null && seen.Add(bareArgument.Key))
                {
                    arguments.Add(bareArgument);
                }
            }

            previousNonEmptyLine = line;
        }

        return arguments;
    }
}

