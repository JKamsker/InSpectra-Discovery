namespace InSpectra.Discovery.Tool.Help;

using System.Text.RegularExpressions;

internal static partial class OptionValueInferenceSupport
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

    [GeneratedRegex(@"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+", RegexOptions.Compiled)]
    private static partial Regex CamelCaseTokenRegex();
}

