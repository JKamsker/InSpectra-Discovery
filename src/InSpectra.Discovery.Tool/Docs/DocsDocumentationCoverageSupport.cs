namespace InSpectra.Discovery.Tool.Docs;

using System.Text.Json.Nodes;

internal static class DocsDocumentationCoverageSupport
{
    public static DocumentationStats CollectStats(JsonObject openCli)
    {
        var stats = new DocumentationStats();
        AddOptionStats(stats, "[root]", GetVisibleItems(openCli["options"] as JsonArray));
        AddArgumentStats(stats, "[root]", GetVisibleItems(openCli["arguments"] as JsonArray));
        AddCommandStats(stats, string.Empty, GetVisibleItems(openCli["commands"] as JsonArray));
        return stats;
    }

    private static void AddCommandStats(DocumentationStats stats, string parentPath, IReadOnlyList<JsonObject> commands)
    {
        foreach (var command in commands)
        {
            var commandName = command["name"]?.GetValue<string>() ?? string.Empty;
            var commandPath = string.IsNullOrWhiteSpace(parentPath) ? commandName : $"{parentPath} {commandName}";
            stats.VisibleCommands++;
            if (HasText(command["description"]))
            {
                stats.DescribedCommands++;
            }
            else
            {
                stats.MissingCommandDescriptions.Add(commandPath);
            }

            AddOptionStats(stats, commandPath, GetVisibleItems(command["options"] as JsonArray));
            AddArgumentStats(stats, commandPath, GetVisibleItems(command["arguments"] as JsonArray));

            var children = GetVisibleItems(command["commands"] as JsonArray);
            if (children.Length == 0)
            {
                stats.VisibleLeafCommands++;
                var examples = (command["examples"] as JsonArray)?.Where(HasText).ToList() ?? [];
                if (examples.Count > 0)
                {
                    stats.LeafCommandsWithExamples++;
                }
                else
                {
                    stats.MissingLeafExamples.Add(commandPath);
                }
            }

            AddCommandStats(stats, commandPath, children);
        }
    }

    private static void AddOptionStats(DocumentationStats stats, string location, IReadOnlyList<JsonObject> options)
    {
        foreach (var option in options)
        {
            stats.VisibleOptions++;
            var optionName = option["name"]?.GetValue<string>() ?? string.Empty;
            var qualifiedName = string.IsNullOrWhiteSpace(location) ? optionName : $"{location} {optionName}";
            if (HasText(option["description"]))
            {
                stats.DescribedOptions++;
            }
            else
            {
                stats.MissingOptionDescriptions.Add(qualifiedName);
            }
        }
    }

    private static void AddArgumentStats(DocumentationStats stats, string location, IReadOnlyList<JsonObject> arguments)
    {
        foreach (var argument in arguments)
        {
            stats.VisibleArguments++;
            var argumentName = argument["name"]?.GetValue<string>() ?? string.Empty;
            var qualifiedName = string.IsNullOrWhiteSpace(location) ? $"<{argumentName}>" : $"{location} <{argumentName}>";
            if (HasText(argument["description"]))
            {
                stats.DescribedArguments++;
            }
            else
            {
                stats.MissingArgumentDescriptions.Add(qualifiedName);
            }
        }
    }

    private static JsonObject[] GetVisibleItems(JsonArray? items)
        => items?.OfType<JsonObject>().Where(item => item["hidden"]?.GetValue<bool?>() != true).ToArray() ?? [];

    private static bool HasText(JsonNode? value)
        => value is JsonValue jsonValue &&
           jsonValue.TryGetValue<string>(out var text) &&
           !string.IsNullOrWhiteSpace(text);
}

internal sealed class DocumentationStats
{
    public int VisibleCommands { get; set; }
    public int DescribedCommands { get; set; }
    public int VisibleOptions { get; set; }
    public int DescribedOptions { get; set; }
    public int VisibleArguments { get; set; }
    public int DescribedArguments { get; set; }
    public int VisibleLeafCommands { get; set; }
    public int LeafCommandsWithExamples { get; set; }
    public List<string> MissingCommandDescriptions { get; } = [];
    public List<string> MissingOptionDescriptions { get; } = [];
    public List<string> MissingArgumentDescriptions { get; } = [];
    public List<string> MissingLeafExamples { get; } = [];

    public bool IsComplete =>
        VisibleCommands == DescribedCommands &&
        VisibleOptions == DescribedOptions &&
        VisibleArguments == DescribedArguments &&
        VisibleLeafCommands == LeafCommandsWithExamples;
}

