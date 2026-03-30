internal static class ToolHelpUsageArgumentExtractionSupport
{
    public static IReadOnlyList<ToolHelpItem> Extract(
        string commandName,
        string commandPath,
        IReadOnlyList<string> usageLines,
        bool hasChildCommands)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arguments = new List<ToolHelpItem>();
        var invocation = string.IsNullOrWhiteSpace(commandPath)
            ? commandName
            : $"{commandName} {commandPath}";
        string? previousNonEmptyLine = null;

        foreach (var line in usageLines)
        {
            var lineArguments = new List<ToolHelpItem>();
            var stopLine = false;
            foreach (var argument in ToolHelpBracketedUsageArgumentSupport.Extract(line, seen, hasChildCommands, out stopLine))
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

            if (ToolHelpBareUsageArgumentSupport.LooksLikeWrappedUsageValueContinuation(previousNonEmptyLine, line))
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (!ToolHelpBareUsageArgumentSupport.LooksLikeExampleLabel(previousNonEmptyLine))
            {
                var bareArgument = ToolHelpBareUsageArgumentSupport.TryExtract(invocation, line, hasChildCommands);
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
