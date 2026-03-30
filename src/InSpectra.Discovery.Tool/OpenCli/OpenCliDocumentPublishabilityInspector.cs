using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static partial class OpenCliDocumentPublishabilityInspector
{
    public static bool HasPublishableSurface(JsonObject document)
        => HasVisibleItems(document["options"] as JsonArray)
            || HasVisibleItems(document["arguments"] as JsonArray)
            || HasVisibleCommandSurface(document["commands"] as JsonArray);

    public static bool LooksLikeInventoryOnlyCommandShellDocument(JsonObject document)
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

    public static bool ContainsErrorText(JsonObject document)
    {
        var texts = new List<string>();
        CollectTextFields(document, texts, depth: 0);
        return texts.Any(LooksLikeErrorText)
            || texts.Any(ContainsSandboxPathLeak);
    }

    public static int CountTotalCommands(JsonObject node)
    {
        var count = 0;
        if (node["commands"] is not JsonArray commands)
        {
            return count;
        }

        count += commands.Count;
        foreach (var command in commands.OfType<JsonObject>())
        {
            count += CountTotalCommands(command);
            if (count > 500)
            {
                return count;
            }
        }

        return count;
    }

    public static bool ContainsBoxDrawingCommandNames(JsonObject node)
        => ContainsBoxDrawingCommandNamesRecursive(node, depth: 0);

    public static bool LooksLikeNonPublishableTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var trimmed = title.Trim();
        return trimmed.Length > 120
            || trimmed.Contains('\n', StringComparison.Ordinal)
            || trimmed.Contains(". ", StringComparison.Ordinal)
            || TitleNoiseRegex().IsMatch(trimmed)
            || PathOrUrlRegex().IsMatch(trimmed)
            || ErrorLikeTitleRegex().IsMatch(trimmed)
            || ContainsBoxDrawingOrBlockChars(trimmed);
    }

    public static bool LooksLikeNonPublishableDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var trimmed = description.Trim();
        return DescriptionNoiseRegex().IsMatch(trimmed)
            || trimmed.Contains("\n   at ", StringComparison.Ordinal)
            || trimmed.Contains("\nat ", StringComparison.Ordinal)
            || trimmed.Contains("/tmp/inspectra-", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("/usr/share/dotnet/", StringComparison.OrdinalIgnoreCase)
            || ContainsBoxDrawingOrBlockChars(trimmed);
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

    private static bool ContainsBoxDrawingOrBlockChars(string text)
        => text.Any(ch => ch is '│' or '┌' or '┐' or '└' or '┘' or '├' or '┤' or '┬' or '┴' or '┼' or '─'
            or '═' or '║' or '╔' or '╗' or '╚' or '╝' or '╠' or '╣' or '╦' or '╩' or '╬'
            or '█' or '▀' or '▄' or '▌' or '▐' or '░' or '▒' or '▓'
            or '■' or '╒' or '╓' or '╕' or '╖' or '╘' or '╙' or '╛' or '╜' or '╞' or '╟' or '╡' or '╢' or '╤' or '╥' or '╧' or '╨' or '╪' or '╫');

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

    private static bool ContainsSandboxPathLeak(string text)
        => text.Contains("/tmp/inspectra-", StringComparison.OrdinalIgnoreCase);

    private static void CollectTextFields(JsonObject node, List<string> texts, int depth)
    {
        if (depth > 5)
        {
            return;
        }

        foreach (var property in node)
        {
            if (property.Value is JsonValue value && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text)
                && property.Key is "description" or "name")
            {
                texts.Add(text);
            }
            else if (property.Value is JsonObject child)
            {
                CollectTextFields(child, texts, depth + 1);
            }
            else if (property.Value is JsonArray array)
            {
                foreach (var element in array.OfType<JsonObject>())
                {
                    CollectTextFields(element, texts, depth + 1);
                }
            }
        }
    }

    private static bool LooksLikeErrorText(string text)
        => ErrorTextRegex().IsMatch(text);

    private static string? GetString(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    [GeneratedRegex(@"^(?:usage\b|version:|help:|unhandled exception\b|unexpected argument\b|invalid arguments?\b|now listening on\b|application started\b|hosting failed to start\b|starting\s+\w+\b|missing\s+\w+\b|\d{4}-\d{2}-\d{2}[T ]|\[(?:info|error|warn|information|debug|fatal)\]|(?:fail|error|info|warn|dbug|crit):\s)|\b(?:Unhandled exception|Unexpected argument|Invalid arguments|Now listening on|Application started|Hosting failed to start)\b|\bDefaulting to\b.*\brequires\b.+\bruntime\b|\bvia:\b.+(?:--|/)|\bcopyright\b|\(c\)\s+\w+|\ball rights reserved\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TitleNoiseRegex();

    [GeneratedRegex(@"https?://|[A-Za-z]:\\|/tmp/|/usr/|\.dll\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PathOrUrlRegex();

    [GeneratedRegex(@"\b(?:System\.\w+Exception|Unhandled\s+[Ee]xception|FileNotFoundException|ArgumentNullException|NullReferenceException|StackOverflowException|InvalidOperationException)\b|^\s*at\s+\S+\.\S+\(|^\s*---\s*>", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex ErrorTextRegex();

    [GeneratedRegex(@"Unhandled exception\b|Hosting failed to start\b|Now listening on:|Application started\.|Microsoft\.Hosting\.Lifetime|System\.[A-Za-z]+Exception\b|Traceback \(most recent call last\):|Press any key to exit|Cannot read keys when either application does not have a console|You must install or update \.NET|A fatal error was encountered|It was not possible to find any compatible framework version|required to execute the application was not found|\bMCP\b.*\btransport\b|\btransport\b.*\bMCP\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex DescriptionNoiseRegex();

    [GeneratedRegex(@"^(?:Error|Warning)\b|^There was an error\b|\bfatal error\b|\berror creating\b|\berror while\b|\bPlease try the command\b|\blibhostpolicy\.so\b|\bAttempt to copy\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ErrorLikeTitleRegex();

    [GeneratedRegex(@"^[|/\\]{1,2}$|\.cs:line\s+\d+|^at\s+\S+\.\S+\(", RegexOptions.Compiled)]
    private static partial Regex GarbageCommandNameRegex();
}
