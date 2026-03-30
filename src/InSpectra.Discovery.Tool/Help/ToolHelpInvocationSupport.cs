internal static class ToolHelpInvocationSupport
{
    public static IReadOnlyList<string[]> BuildHelpInvocations(IReadOnlyList<string> commandSegments)
    {
        var invocations = new List<string[]>
        {
            commandSegments.Concat(new[] { "--help" }).ToArray(),
            commandSegments.Concat(new[] { "-h" }).ToArray(),
            commandSegments.Concat(new[] { "-?" }).ToArray(),
            commandSegments.Concat(new[] { "/help" }).ToArray(),
            commandSegments.Concat(new[] { "/?" }).ToArray(),
        };

        invocations.AddRange(BuildKeywordHelpInvocations(commandSegments));
        invocations.Add(commandSegments.ToArray());

        return invocations
            .Distinct(new ToolHelpInvocationComparer())
            .ToArray();
    }

    public static string GetCommandKey(IReadOnlyList<string> commandSegments)
        => commandSegments.Count == 0 ? string.Empty : string.Join(' ', commandSegments);

    private static IEnumerable<string[]> BuildKeywordHelpInvocations(IReadOnlyList<string> commandSegments)
    {
        if (commandSegments.Count == 0)
        {
            yield return ["help"];
            yield break;
        }

        yield return (new[] { "help" }).Concat(commandSegments).ToArray();

        for (var index = 1; index < commandSegments.Count; index++)
        {
            yield return commandSegments.Take(index)
                .Concat(new[] { "help" })
                .Concat(commandSegments.Skip(index))
                .ToArray();
        }

        yield return commandSegments.Concat(new[] { "help" }).ToArray();
    }
}
