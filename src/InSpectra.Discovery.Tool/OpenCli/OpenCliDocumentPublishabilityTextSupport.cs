using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static partial class OpenCliDocumentPublishabilityInspector
{
    private static bool ContainsErrorTextCore(JsonObject document)
    {
        var texts = new List<string>();
        CollectTextFields(document, texts, depth: 0);
        return texts.Any(LooksLikeErrorText)
            || texts.Any(ContainsSandboxPathLeak);
    }

    private static bool LooksLikeNonPublishableTitleCore(string? title)
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

    private static bool LooksLikeNonPublishableDescriptionCore(string? description)
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

    private static bool ContainsBoxDrawingOrBlockChars(string text)
        => text.Any(ch => ch is '│' or '┌' or '┐' or '└' or '┘' or '├' or '┤' or '┬' or '┴' or '┼' or '─'
            or '═' or '║' or '╔' or '╗' or '╚' or '╝' or '╠' or '╣' or '╦' or '╩' or '╬'
            or '█' or '▀' or '▄' or '▌' or '▐' or '░' or '▒' or '▓'
            or '■' or '╒' or '╓' or '╕' or '╖' or '╘' or '╙' or '╛' or '╜' or '╞' or '╟' or '╡' or '╢' or '╤' or '╥' or '╧' or '╨' or '╪' or '╫');

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
}
