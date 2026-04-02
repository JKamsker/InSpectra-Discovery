namespace InSpectra.Discovery.Tool.OpenCli.Documents;

using System.Text.Json.Nodes;

internal static class OpenCliStartupHookDirectiveHostNormalizationSupport
{
    public static void Normalize(JsonObject document)
    {
        if (!IsStartupHookDocument(document)
            || document["commands"] is not JsonArray commands)
        {
            return;
        }

        var topLevelCommands = commands.OfType<JsonObject>().ToArray();
        if (topLevelCommands.Length == 0
            || !topLevelCommands.Any(IsDirectiveHostCommand))
        {
            return;
        }

        var wrapperCommand = ResolveWrapperCommand(document, topLevelCommands);
        if (wrapperCommand is null)
        {
            return;
        }

        ReplaceRootArray(document, "commands", wrapperCommand["commands"] as JsonArray);
        ReplaceRootArray(document, "options", wrapperCommand["options"] as JsonArray);
        ReplaceRootArray(document, "arguments", wrapperCommand["arguments"] as JsonArray);
    }

    private static bool IsStartupHookDocument(JsonObject document)
        => string.Equals(
            document["x-inspectra"]?["artifactSource"]?.GetValue<string>(),
            "startup-hook",
            StringComparison.OrdinalIgnoreCase);

    private static JsonObject? ResolveWrapperCommand(JsonObject document, IReadOnlyList<JsonObject> topLevelCommands)
    {
        var parsedTitle = document["x-inspectra"]?["cliParsedTitle"]?.GetValue<string>();
        var normalizedParsedTitle = NormalizeComparableName(parsedTitle);

        var wrapperCandidates = topLevelCommands
            .Where(command => command["name"]?.GetValue<string>()?.StartsWith("#!", StringComparison.Ordinal) is true)
            .ToArray();

        var nestedWrapperCandidates = wrapperCandidates
            .Where(candidate => candidate["commands"] is JsonArray nestedCommands && nestedCommands.Count > 0)
            .ToArray();

        foreach (var candidate in nestedWrapperCandidates)
        {
            var candidateName = NormalizeComparableName(candidate["name"]?.GetValue<string>());
            if (!string.IsNullOrWhiteSpace(normalizedParsedTitle)
                && string.Equals(candidateName, normalizedParsedTitle, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        if (nestedWrapperCandidates.Length == 1)
        {
            return nestedWrapperCandidates[0];
        }

        return wrapperCandidates.Length == 1
            ? wrapperCandidates[0]
            : null;
    }

    private static bool IsDirectiveHostCommand(JsonObject command)
        => command["name"]?.GetValue<string>()?.StartsWith("#", StringComparison.Ordinal) is true;

    private static string NormalizeComparableName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return name
            .Trim()
            .TrimStart('#', '!')
            .Trim();
    }

    private static void ReplaceRootArray(JsonObject document, string propertyName, JsonArray? source)
    {
        if (source is null || source.Count == 0)
        {
            document.Remove(propertyName);
            return;
        }

        var cloned = source.DeepClone() as JsonArray;
        if (cloned is null || cloned.Count == 0)
        {
            document.Remove(propertyName);
            return;
        }

        document[propertyName] = cloned;
    }
}
