namespace InSpectra.Discovery.Tool.StaticAnalysis;

internal static class StaticCommandDefinitionSupport
{
    public static void UpsertBest(
        IDictionary<string, StaticCommandDefinition> commands,
        string key,
        StaticCommandDefinition definition)
    {
        if (!commands.TryGetValue(key, out var existing) || Score(definition) > Score(existing))
        {
            commands[key] = definition;
        }
    }

    public static void UpsertBest(
        IDictionary<string, StaticCommandDefinition> commands,
        StaticCommandDefinition definition)
        => UpsertBest(commands, definition.Name ?? string.Empty, definition);

    public static int Score(StaticCommandDefinition definition)
        => definition.Values.Count + definition.Options.Count + (definition.Description is null ? 0 : 1);
}

