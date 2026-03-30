namespace InSpectra.Discovery.Tool.Help;

internal static class ToolHelpCommandSignatureSupport
{
    public static string NormalizeCommandKey(string key)
    {
        var normalizedKey = key.Trim();
        var rawSegments = normalizedKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var segments = new List<string>();
        for (var index = 0; index < rawSegments.Length; index++)
        {
            var aliases = new List<string> { rawSegments[index].TrimEnd(',', ':') };
            while (rawSegments[index].EndsWith(",", StringComparison.Ordinal) && index + 1 < rawSegments.Length)
            {
                index++;
                aliases.Add(rawSegments[index].TrimEnd(',', ':'));
            }

            segments.Add(aliases
                .Where(alias => alias.Length > 0)
                .OrderByDescending(alias => alias.Length)
                .FirstOrDefault() ?? string.Empty);
        }

        var normalized = segments
            .TakeWhile(segment => !segment.StartsWith("<", StringComparison.Ordinal)
                && !segment.StartsWith("[", StringComparison.Ordinal)
                && !segment.StartsWith("(", StringComparison.Ordinal)
                && !segment.StartsWith("-", StringComparison.Ordinal)
                && !segment.StartsWith("/", StringComparison.Ordinal))
            .Where(segment => segment.Length > 0)
            .ToArray();
        return normalized.Length == 0 || normalized.Any(segment => !LooksLikeCommandSegment(segment))
            ? string.Empty
            : string.Join(' ', normalized);
    }

    public static bool LooksLikeCommandDescription(string description)
    {
        var trimmed = description.TrimStart();
        while (trimmed.StartsWith("(", StringComparison.Ordinal))
        {
            var closingIndex = trimmed.IndexOf(')');
            if (closingIndex < 0)
            {
                break;
            }

            trimmed = trimmed[(closingIndex + 1)..].TrimStart();
        }

        return trimmed.Length > 0 && char.IsLetter(trimmed[0]);
    }

    public static bool IsBuiltinAuxiliaryCommand(string key)
        => string.Equals(key, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "version", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeCommandItemLine(string rawLine)
    {
        var trimmedStart = rawLine.TrimStart();
        if (!trimmedStart.StartsWith(">", StringComparison.Ordinal))
        {
            return rawLine;
        }

        var commandLine = trimmedStart[1..].TrimStart();
        var separatorIndex = commandLine.IndexOf(':');
        if (separatorIndex < 0)
        {
            return commandLine;
        }

        var commandKey = commandLine[..separatorIndex].Trim();
        var commandDescription = commandLine[(separatorIndex + 1)..].Trim();
        return string.IsNullOrWhiteSpace(commandDescription)
            ? commandKey
            : $"{commandKey}  {commandDescription}";
    }

    public static bool LooksLikeMarkdownTableLine(string line)
        => line.StartsWith("|", StringComparison.Ordinal)
            && line.EndsWith("|", StringComparison.Ordinal)
            && line.Count(ch => ch == '|') >= 2;

    private static bool LooksLikeCommandSegment(string segment)
        => segment.Length > 0
            && char.IsLetter(segment[0])
            && !segment.StartsWith("CommandLine.", StringComparison.Ordinal)
            && segment.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or ':' or '+');
}

