using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static partial class OpenCliDocumentPublishabilityInspector
{
    private static bool ContainsBoxDrawingCommandNamesCore(JsonObject node)
        => ContainsBoxDrawingCommandNamesRecursive(node, depth: 0);

    private static bool ContainsBoxDrawingCommandNamesRecursive(JsonObject node, int depth)
    {
        if (depth > 8 || node["commands"] is not JsonArray commands)
        {
            return false;
        }

        foreach (var command in commands.OfType<JsonObject>())
        {
            var name = GetString(command["name"]);
            if (name is not null && (ContainsBoxDrawingOrBlockChars(name) || LooksLikeGarbageCommandName(name)))
            {
                return true;
            }

            if (ContainsBoxDrawingCommandNamesRecursive(command, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeGarbageCommandName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed is "|" or "||")
        {
            return true;
        }

        if (trimmed.StartsWith("| ", StringComparison.Ordinal) && trimmed.Contains(':', StringComparison.Ordinal))
        {
            return true;
        }

        return GarbageCommandNameRegex().IsMatch(trimmed);
    }

    [GeneratedRegex(@"^[|/\\]{1,2}$|\.cs:line\s+\d+|^at\s+\S+\.\S+\(", RegexOptions.Compiled)]
    private static partial Regex GarbageCommandNameRegex();
}
