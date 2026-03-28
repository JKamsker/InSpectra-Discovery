using System.Text.RegularExpressions;

internal static partial class ToolHelpCommandPrototypeSupport
{
    public static bool AllowsBlankDescriptionLine(string key)
        => LooksLikeCommandPrototype(key) || LooksLikeBareCommandToken(key);

    public static bool LooksLikeCommandPrototype(string key)
    {
        if (LooksLikeCommandAliasList(key))
        {
            return true;
        }

        var tokens = SplitTokens(key);
        return tokens.Length > 1
            && LooksLikeCommandSegment(tokens[0])
            && tokens.Skip(1).All(LooksLikePrototypeToken);
    }

    public static bool LooksLikeBareShortLongOptionRow(string rawLine)
        => BareShortLongOptionRowRegex().IsMatch(rawLine);

    private static bool LooksLikeCommandAliasList(string key)
    {
        if (!key.Contains(',', StringComparison.Ordinal))
        {
            return false;
        }

        var tokens = SplitTokens(key);
        return tokens.Length > 1
            && tokens.All(token => LooksLikeCommandSegment(token.TrimEnd(',', ':')));
    }

    private static bool LooksLikeBareCommandToken(string key)
    {
        var tokens = SplitTokens(key);
        return tokens.Length == 1 && LooksLikeCommandSegment(tokens[0]);
    }

    private static string[] SplitTokens(string key)
        => key.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool LooksLikePrototypeToken(string token)
    {
        var trimmed = token.Trim(',', ':');
        if (trimmed.Length == 0)
        {
            return false;
        }

        return trimmed.StartsWith("<", StringComparison.Ordinal)
            || trimmed.StartsWith("[", StringComparison.Ordinal)
            || trimmed.StartsWith("(", StringComparison.Ordinal)
            || trimmed.EndsWith("?", StringComparison.Ordinal)
            || trimmed.EndsWith("*", StringComparison.Ordinal)
            || trimmed.EndsWith("+", StringComparison.Ordinal)
            || trimmed.Contains('<', StringComparison.Ordinal)
            || trimmed.Contains('[', StringComparison.Ordinal)
            || trimmed.Contains('(', StringComparison.Ordinal)
            || trimmed.Contains('|', StringComparison.Ordinal)
            || string.Equals(trimmed, "...", StringComparison.Ordinal);
    }

    private static bool LooksLikeCommandSegment(string segment)
        => segment.Length > 0
            && char.IsLetter(segment[0])
            && segment.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or ':' or '+');

    [GeneratedRegex(@"^\s*[A-Za-z0-9\?]\s*,\s*[A-Za-z][A-Za-z0-9_.-]*\s{2,}\S", RegexOptions.Compiled)]
    private static partial Regex BareShortLongOptionRowRegex();
}
