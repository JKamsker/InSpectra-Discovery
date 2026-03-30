using System.Text.RegularExpressions;

internal static partial class ToolHelpSignatureNormalizer
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

    public static string NormalizeArgumentKey(string key)
        => key.Trim().TrimStart('[', '<').TrimEnd(']', '>');

    public static string NormalizeOptionSignatureKey(string key)
    {
        key = NormalizeInlineOptionPlaceholders(key);
        var matches = OptionTokenRegex().Matches(key);
        if (matches.Count == 0)
        {
            return key.Trim();
        }

        var trailing = key[(matches[^1].Index + matches[^1].Length)..].Trim();
        if (string.IsNullOrWhiteSpace(trailing)
            || trailing.StartsWith("=", StringComparison.Ordinal)
            || trailing.StartsWith(":", StringComparison.Ordinal))
        {
            return key.Trim();
        }

        if (trailing.StartsWith("<", StringComparison.Ordinal) || trailing.StartsWith("[", StringComparison.Ordinal))
        {
            return $"{key[..(matches[^1].Index + matches[^1].Length)].Trim()} {trailing}";
        }

        if (!IsBareOptionPlaceholder(trailing))
        {
            return key.Trim();
        }

        return $"{key[..(matches[^1].Index + matches[^1].Length)].Trim()} <{trailing.ToUpperInvariant()}>";
    }

    public static bool LooksLikeOptionSignature(string key)
    {
        var remaining = key.Trim();
        var sawToken = false;
        while (remaining.Length > 0)
        {
            var match = OptionTokenRegex().Match(remaining);
            if (!match.Success || match.Index != 0)
            {
                return false;
            }

            sawToken = true;
            remaining = remaining[match.Length..].TrimStart();
            if (remaining.Length == 0)
            {
                return true;
            }

            if (remaining.StartsWith(",", StringComparison.Ordinal) || remaining.StartsWith("|", StringComparison.Ordinal))
            {
                remaining = remaining[1..].TrimStart();
                continue;
            }

            if (remaining.StartsWith("<", StringComparison.Ordinal)
                || remaining.StartsWith("[", StringComparison.Ordinal)
                || remaining.StartsWith("=", StringComparison.Ordinal)
                || remaining.StartsWith(":", StringComparison.Ordinal)
                || IsBareOptionPlaceholder(remaining))
            {
                return true;
            }

            return false;
        }

        return sawToken;
    }

    public static bool TryExtractLeadingAliasFromDescription(string? description, out string alias, out string? normalizedDescription)
    {
        alias = string.Empty;
        normalizedDescription = description;
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var trimmed = description.Trim().TrimStart('|').TrimStart();
        if (!TryConsumeLeadingOptionAliasGroup(trimmed, out alias, out var remainder))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(remainder))
        {
            normalizedDescription = null;
            return true;
        }

        if (!char.IsWhiteSpace(remainder[0]))
        {
            return false;
        }

        var trimmedRemainder = remainder.TrimStart();
        var separatorIndex = trimmedRemainder.IndexOf(' ');
        var candidatePlaceholder = separatorIndex >= 0
            ? trimmedRemainder[..separatorIndex]
            : trimmedRemainder;
        var candidateDescription = separatorIndex >= 0
            ? trimmedRemainder[(separatorIndex + 1)..].TrimStart()
            : null;
        if (LooksLikeSplitColumnPlaceholder(candidatePlaceholder)
            || candidatePlaceholder.StartsWith("<", StringComparison.Ordinal)
            || candidatePlaceholder.StartsWith("[", StringComparison.Ordinal))
        {
            alias = NormalizeOptionSignatureKey($"{alias} {candidatePlaceholder}");
            normalizedDescription = string.IsNullOrWhiteSpace(candidateDescription) ? null : candidateDescription;
            return true;
        }

        normalizedDescription = trimmedRemainder;
        return true;
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

    private static string NormalizeInlineOptionPlaceholders(string key)
        => InterleavedOptionPlaceholderRegex().Replace(
            key,
            match => $"{match.Groups["option"].Value} <{match.Groups["placeholder"].Value.ToUpperInvariant()}>");

    private static bool LooksLikeCommandSegment(string segment)
        => segment.Length > 0
            && char.IsLetter(segment[0])
            && !segment.StartsWith("CommandLine.", StringComparison.Ordinal)
            && segment.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or ':' or '+');

    private static bool TryConsumeLeadingOptionAliasGroup(string text, out string aliasGroup, out string remainder)
    {
        aliasGroup = string.Empty;
        remainder = text;

        var match = LeadingOptionAliasGroupRegex().Match(text);
        if (!match.Success || match.Index != 0)
        {
            return false;
        }

        aliasGroup = string.Join(
            " | ",
            match.Groups["group"].Value
                .Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(segment => !string.IsNullOrWhiteSpace(segment)));
        remainder = text[match.Length..];
        return !string.IsNullOrWhiteSpace(aliasGroup);
    }

    private static bool IsBareOptionPlaceholder(string value)
        => !string.IsNullOrWhiteSpace(value)
            && !value.Contains(' ', StringComparison.Ordinal)
            && !value.StartsWith("<", StringComparison.Ordinal)
            && !value.StartsWith("[", StringComparison.Ordinal)
            && !value.StartsWith("-", StringComparison.Ordinal)
            && !value.StartsWith("/", StringComparison.Ordinal);

    private static bool LooksLikeSplitColumnPlaceholder(string value)
        => IsBareOptionPlaceholder(value)
            && value.Any(char.IsLetter)
            && value.Where(char.IsLetter).All(char.IsUpper);

    [GeneratedRegex(@"(?<option>(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*))", RegexOptions.Compiled)]
    private static partial Regex OptionTokenRegex();

    [GeneratedRegex(@"^(?<group>(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*)(?:\s*(?:\||,)\s*(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*))*)", RegexOptions.Compiled)]
    private static partial Regex LeadingOptionAliasGroupRegex();

    [GeneratedRegex(@"(?<option>(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*))\s+(?<placeholder>[A-Za-z][A-Za-z0-9_\.\-]*)(?=\s*(?:[,|]\s*(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*)|$))", RegexOptions.Compiled)]
    private static partial Regex InterleavedOptionPlaceholderRegex();
}
