using System.Text.RegularExpressions;

internal static partial class ToolHelpOptionSignatureSupport
{
    private static readonly HashSet<string> ValueLikeOptionNameTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "access", "address", "api", "alias", "assembly", "baseline", "byte", "bytes", "branch", "certificate",
        "cert", "channel", "code", "codes", "column", "columns", "component", "config", "configuration", "conn",
        "connection", "comment", "comments", "count", "culture", "database", "directories", "dir", "directory",
        "dll", "duration", "email", "env", "environment", "etw", "expiry", "exclude", "file", "files", "filter",
        "folder", "format", "factory", "feature", "guid", "host", "id", "ids", "index", "indexes", "include",
        "input", "justification", "key", "kind", "language", "level", "license", "log", "migration", "model",
        "modifier", "method", "namespace", "name", "notes", "output", "package", "param", "parser", "password",
        "path", "pattern", "plugin", "policy", "port", "post", "producer", "producers", "provider", "project",
        "property", "prefix", "regex", "region", "regions", "repo", "repository", "result", "runtime", "rule",
        "save", "schema", "schemas", "search", "service", "server", "solution", "source", "status", "subscription",
        "table", "tables", "target", "targets", "template", "threshold", "thread", "threads", "thumbprint",
        "timeout", "token", "topic", "tool", "trace", "type", "uri", "url", "value", "version", "xml", "xsl",
        "yaml", "yml", "zip", "size",
    };

    public static ToolHelpOptionSignature Parse(string key)
    {
        var aliases = new List<string>();
        var placeholders = UsageArgumentRegex().Matches(key)
            .Select(match => match.Groups["name"].Value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();
        var barePlaceholder = placeholders.Length == 0
            ? ExtractBareOptionPlaceholder(key)
            : null;

        var keyForAliasParsing = StripBracketedPlaceholders(key);
        foreach (var segment in keyForAliasParsing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string? previousToken = null;
            foreach (var pipeSegment in segment.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var token = TryParseOptionToken(pipeSegment, previousToken);
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                aliases.Add(token);
                previousToken = token;
            }
        }

        var primary = aliases
            .OrderByDescending(name => name.StartsWith("--", StringComparison.Ordinal) || name.StartsWith("/", StringComparison.Ordinal))
            .ThenByDescending(name => name.Length)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return new ToolHelpOptionSignature(
            PrimaryName: primary,
            Aliases: aliases
                .Where(alias => !string.Equals(alias, primary, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ArgumentName: NormalizeOptionArgumentName(placeholders.FirstOrDefault() ?? barePlaceholder, primary),
            ArgumentRequired: !key.Contains("[", StringComparison.Ordinal));
    }

    public static IEnumerable<string> EnumerateTokens(ToolHelpOptionSignature signature)
    {
        if (!string.IsNullOrWhiteSpace(signature.PrimaryName))
        {
            yield return signature.PrimaryName;
        }

        foreach (var alias in signature.Aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)))
        {
            yield return alias;
        }
    }

    public static bool LooksLikeOptionPlaceholder(string value)
        => value.StartsWith("-", StringComparison.Ordinal)
            || value.StartsWith("/", StringComparison.Ordinal)
            || (value.Contains('|', StringComparison.Ordinal) && OptionTokenRegex().Match(value).Success)
            || (value.Contains('=', StringComparison.Ordinal) && OptionTokenRegex().Match(value).Success);

    public static bool AppearsInOptionClause(string line, Match match)
    {
        var index = match.Index - 1;
        while (index >= 0 && char.IsWhiteSpace(line[index]))
        {
            index--;
        }

        if (index < 0)
        {
            return false;
        }

        if (line[index] is '"' or '\'')
        {
            index--;
            while (index >= 0 && char.IsWhiteSpace(line[index]))
            {
                index--;
            }
        }

        if (index < 0)
        {
            return false;
        }

        var tokenEnd = index;
        while (index >= 0 && !char.IsWhiteSpace(line[index]) && line[index] is not '[' and not '(' and not '{')
        {
            index--;
        }

        var candidate = line[(index + 1)..(tokenEnd + 1)].TrimEnd('=', ':');
        return candidate.Length > 0 && OptionTokenRegex().Match(candidate).Success;
    }

    public static string NormalizeArgumentName(string key)
        => key.Replace('-', '_').ToUpperInvariant();

    public static bool HasValueLikeOptionName(string primaryOption)
        => GetOptionNameTokens(primaryOption).Any(IsValueLikeOptionToken);

    public static string? InferArgumentNameFromOption(string? primaryOption)
    {
        if (string.IsNullOrWhiteSpace(primaryOption))
        {
            return null;
        }

        var token = primaryOption.TrimStart('-', '/');
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var separator = token.IndexOfAny(['=', ':']);
        if (separator >= 0)
        {
            token = token[..separator];
        }

        return NormalizeArgumentName(token);
    }

    private static string? NormalizeOptionArgumentName(string? rawPlaceholder, string? primaryOption)
    {
        if (string.IsNullOrWhiteSpace(rawPlaceholder))
        {
            return null;
        }

        if (rawPlaceholder.Contains('|', StringComparison.Ordinal) || LooksLikeOptionPlaceholder(rawPlaceholder))
        {
            return InferArgumentNameFromOption(primaryOption);
        }

        return ToolHelpArgumentNodeBuilder.TryParseArgumentSignature(rawPlaceholder, out var signature)
            ? signature.Name
            : InferArgumentNameFromOption(primaryOption);
    }

    private static IReadOnlyList<string> GetOptionNameTokens(string primaryOption)
    {
        var trimmed = primaryOption.TrimStart('-', '/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return [];
        }

        var separator = trimmed.IndexOfAny(['=', ':']);
        if (separator >= 0)
        {
            trimmed = trimmed[..separator];
        }

        return trimmed
            .Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(SplitCamelCaseTokens)
            .Where(token => token.Length > 0)
            .Select(token => token.ToLowerInvariant())
            .ToArray();
    }

    private static IEnumerable<string> SplitCamelCaseTokens(string token)
    {
        foreach (Match match in CamelCaseTokenRegex().Matches(token))
        {
            if (match.Length > 0)
            {
                yield return match.Value;
            }
        }
    }

    private static bool IsValueLikeOptionToken(string token)
    {
        if (ValueLikeOptionNameTokens.Contains(token))
        {
            return true;
        }

        if (TrySingularizeToken(token, out var singular) && ValueLikeOptionNameTokens.Contains(singular))
        {
            return true;
        }

        return ValueLikeOptionNameTokens.Any(suffix =>
            token.Length > suffix.Length
            && token.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TrySingularizeToken(string token, out string singular)
    {
        singular = string.Empty;
        if (token.Length <= 2)
        {
            return false;
        }

        if (token.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
        {
            singular = token[..^3] + "y";
            return true;
        }

        if (!token.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        singular = token[..^1];
        return singular.Length > 0;
    }

    private static string StripBracketedPlaceholders(string key)
        => BracketedPlaceholderRegex().Replace(key, string.Empty);

    private static string? ExtractBareOptionPlaceholder(string key)
    {
        var matches = OptionTokenRegex().Matches(key);
        if (matches.Count == 0)
        {
            return null;
        }

        var trailing = key[(matches[^1].Index + matches[^1].Length)..].Trim();
        return IsBareOptionPlaceholder(trailing) ? trailing : null;
    }

    private static bool IsBareOptionPlaceholder(string trailing)
        => !string.IsNullOrWhiteSpace(trailing)
            && !trailing.Contains(' ', StringComparison.Ordinal)
            && !trailing.StartsWith("<", StringComparison.Ordinal)
            && !trailing.StartsWith("[", StringComparison.Ordinal)
            && !trailing.StartsWith("-", StringComparison.Ordinal)
            && !trailing.StartsWith("/", StringComparison.Ordinal);

    private static string? TryParseOptionToken(string segment, string? previousToken)
    {
        var trimmed = segment.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var match = OptionTokenRegex().Match(trimmed);
        if (match.Success && match.Index == 0)
        {
            return match.Value;
        }

        if (!PipeDelimitedOptionAliasSegmentRegex().IsMatch(trimmed))
        {
            return null;
        }

        if (previousToken?.StartsWith("/", StringComparison.Ordinal) == true)
        {
            return "/" + trimmed.TrimStart('-', '/');
        }

        return trimmed.Length == 1
            ? "-" + trimmed.TrimStart('-', '/')
            : "--" + trimmed.TrimStart('-', '/');
    }

    [GeneratedRegex(@"(?<option>(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*))", RegexOptions.Compiled)]
    private static partial Regex OptionTokenRegex();

    [GeneratedRegex(@"(?<all>\[?<(?<name>[^>]+)>\]?)", RegexOptions.Compiled)]
    private static partial Regex UsageArgumentRegex();

    [GeneratedRegex(@"\[(?=[^\]]*(?:-|/))[^\]]+\]", RegexOptions.Compiled)]
    private static partial Regex BareOptionClauseRegex();

    [GeneratedRegex(@"<[^>]*>|\[[^\]]*\]", RegexOptions.Compiled)]
    private static partial Regex BracketedPlaceholderRegex();

    [GeneratedRegex(@"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+", RegexOptions.Compiled)]
    private static partial Regex CamelCaseTokenRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_\.\?\-]*$", RegexOptions.Compiled)]
    private static partial Regex PipeDelimitedOptionAliasSegmentRegex();
}

internal sealed record ToolHelpOptionSignature(
    string? PrimaryName,
    IReadOnlyList<string> Aliases,
    string? ArgumentName,
    bool ArgumentRequired);
