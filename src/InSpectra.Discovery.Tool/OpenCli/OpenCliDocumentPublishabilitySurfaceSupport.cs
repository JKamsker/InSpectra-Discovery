using System.Text.Json.Nodes;

internal static partial class OpenCliDocumentPublishabilityInspector
{
    private static bool HasPublishableSurfaceCore(JsonObject document)
        => HasVisibleItems(document["options"] as JsonArray)
            || HasVisibleItems(document["arguments"] as JsonArray)
            || HasVisibleCommandSurface(document["commands"] as JsonArray);

    private static bool LooksLikeInventoryOnlyCommandShellDocumentCore(JsonObject document)
    {
        if (!string.Equals(GetArtifactSource(document), "crawled-from-help", StringComparison.Ordinal)
            || GetHelpDocumentCount(document) > 1
            || HasVisibleItems(document["options"] as JsonArray)
            || HasVisibleItems(document["arguments"] as JsonArray)
            || document["commands"] is not JsonArray commands)
        {
            return false;
        }

        var nonAuxiliaryCommands = commands
            .OfType<JsonObject>()
            .Where(command => !IsBuiltinAuxiliaryCommand(command))
            .ToArray();
        return nonAuxiliaryCommands.Length > 0 && nonAuxiliaryCommands.All(IsPlaceholderCommandShell);
    }

    private static int CountTotalCommandsCore(JsonObject node)
    {
        var count = 0;
        if (node["commands"] is not JsonArray commands)
        {
            return count;
        }

        count += commands.Count;
        foreach (var command in commands.OfType<JsonObject>())
        {
            count += CountTotalCommandsCore(command);
            if (count > 500)
            {
                return count;
            }
        }

        return count;
    }

    private static bool HasVisibleCommandSurface(JsonArray? commands)
    {
        foreach (var command in commands?.OfType<JsonObject>() ?? [])
        {
            if (IsVisible(command)
                || HasVisibleItems(command["options"] as JsonArray)
                || HasVisibleItems(command["arguments"] as JsonArray)
                || HasVisibleCommandSurface(command["commands"] as JsonArray))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasVisibleItems(JsonArray? items)
        => items?.OfType<JsonObject>().Any(IsVisible) == true;

    private static bool IsVisible(JsonObject node)
        => node["hidden"]?.GetValue<bool?>() != true;

    private static bool IsPlaceholderCommandShell(JsonObject command)
        => !HasVisibleItems(command["options"] as JsonArray)
            && !HasVisibleItems(command["arguments"] as JsonArray)
            && !HasVisibleCommandSurface(command["commands"] as JsonArray);

    private static bool IsBuiltinAuxiliaryCommand(JsonObject command)
    {
        var name = GetString(command["name"]);
        return string.Equals(name, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "version", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetArtifactSource(JsonObject document)
        => document["x-inspectra"] is JsonObject inspectra
            ? GetString(inspectra["artifactSource"])
            : null;

    private static int GetHelpDocumentCount(JsonObject document)
    {
        if (document["x-inspectra"] is not JsonObject inspectra
            || inspectra["helpDocumentCount"] is not JsonValue countValue)
        {
            return int.MaxValue;
        }

        return countValue.TryGetValue<int>(out var count) ? count : int.MaxValue;
    }

    private static string? GetString(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
}
