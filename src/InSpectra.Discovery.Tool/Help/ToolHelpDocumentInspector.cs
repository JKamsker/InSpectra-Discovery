namespace InSpectra.Discovery.Tool.Help;

using System.Text.RegularExpressions;

internal static partial class ToolHelpDocumentInspector
{
    public static int Score(ToolHelpDocument document)
        => document.UsageLines.Count * 10
            + document.Options.Count * 5
            + document.Commands.Count * 5
            + document.Arguments.Count * 3
            + (!string.IsNullOrWhiteSpace(document.CommandDescription) ? 2 : 0)
            + (LooksLikeScorableTitle(document.Title) ? 2 : 0)
            + (!string.IsNullOrWhiteSpace(document.Version) ? 1 : 0);

    public static bool IsCompatible(IReadOnlyList<string> commandSegments, ToolHelpDocument document)
    {
        if (commandSegments.Count == 0)
        {
            return HasStructuredContent(document);
        }

        if (document.UsageLines.Any(line => ContainsPath(line, commandSegments))
            || ContainsPath(document.Title, commandSegments))
        {
            return true;
        }

        if (document.Commands.Count > 0 || LooksLikeDispatcherUsage(document.UsageLines))
        {
            return LooksLikeNestedDispatcher(commandSegments, document);
        }

        return document.Options.Count > 0
            || document.Arguments.Count > 0
            || !string.IsNullOrWhiteSpace(document.CommandDescription);
    }

    public static bool IsCompatible(string[] commandSegments, ToolHelpDocument document)
        => IsCompatible((IReadOnlyList<string>)commandSegments, document);

    public static bool LooksLikeTerminalNonHelpPayload(string? payload)
    {
        var normalized = CommandRuntime.NormalizeConsoleText(payload);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return TerminalNonHelpPayloadRegex().IsMatch(normalized)
            || normalized.Contains("\n   at ", StringComparison.Ordinal)
            || normalized.Contains("\nat ", StringComparison.Ordinal)
            || normalized.Contains("/tmp/inspectra-help-", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/usr/share/dotnet/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBuiltinAuxiliaryCommandPath(string? commandPath)
    {
        var leafSegment = ToolHelpCommandPathSupport.SplitSegments(commandPath).LastOrDefault();
        return string.Equals(leafSegment, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(leafSegment, "version", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBuiltinAuxiliaryInventoryEcho(string? commandPath, ToolHelpDocument document)
        => IsBuiltinAuxiliaryCommandPath(commandPath)
            && document.Commands.Count > 0
            && document.Options.Count == 0
            && document.Arguments.Count == 0;

    private static bool HasStructuredContent(ToolHelpDocument document)
        => document.UsageLines.Count > 0
            || document.Options.Count > 0
            || document.Commands.Count > 0
            || document.Arguments.Count > 0
            || !string.IsNullOrWhiteSpace(document.CommandDescription);

    private static bool LooksLikeDispatcherUsage(IReadOnlyList<string> usageLines)
        => usageLines.Any(line =>
            line.Contains("[command]", StringComparison.OrdinalIgnoreCase)
            || line.Contains("<command>", StringComparison.OrdinalIgnoreCase)
            || line.Contains("[subcommand]", StringComparison.OrdinalIgnoreCase)
            || line.Contains("<subcommand>", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeNestedDispatcher(
        IReadOnlyList<string> commandSegments,
        ToolHelpDocument document)
    {
        if (commandSegments.Count == 0 || document.Commands.Count == 0)
        {
            return false;
        }

        var leafSegment = commandSegments[^1];
        var hasNonAuxiliaryChild = false;
        foreach (var child in document.Commands)
        {
            if (string.Equals(child.Key, "help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(child.Key, "version", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hasNonAuxiliaryChild = true;
            var firstChildSegment = child.Key
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (string.Equals(firstChildSegment, leafSegment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return hasNonAuxiliaryChild;
    }

    private static bool ContainsPath(string? line, IReadOnlyList<string> commandSegments)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < commandSegments.Count)
        {
            return false;
        }

        for (var start = 0; start <= tokens.Length - commandSegments.Count; start++)
        {
            var matched = true;
            for (var index = 0; index < commandSegments.Count; index++)
            {
                if (!string.Equals(tokens[start + index], commandSegments[index], StringComparison.OrdinalIgnoreCase))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeScorableTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var trimmed = title.Trim();
        return !string.Equals(trimmed, "HELP:", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains('[', StringComparison.Ordinal)
            && !trimmed.Contains('<', StringComparison.Ordinal)
            && !trimmed.Contains('|', StringComparison.Ordinal);
    }

    [GeneratedRegex(@"Unhandled exception\b|Hosting failed to start\b|Now listening on:|Application started\.|Microsoft\.Hosting\.Lifetime|System\.[A-Za-z]+Exception\b|Traceback \(most recent call last\):|Press any key to exit|Cannot read keys when either application does not have a console|You must install or update \.NET|A fatal error was encountered|It was not possible to find any compatible framework version|required to execute the application was not found|No executable found matching command|No embedding provider configured|Auto-downloading\b|Downloaded\b|\bMCP\b.*\btransport\b|\btransport\b.*\bMCP\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex TerminalNonHelpPayloadRegex();
}

internal sealed class ToolHelpInvocationComparer : IEqualityComparer<string[]>
{
    public bool Equals(string[]? x, string[]? y)
        => x is not null && y is not null && x.SequenceEqual(y, StringComparer.OrdinalIgnoreCase);

    public int GetHashCode(string[] obj)
    {
        var hash = new HashCode();
        foreach (var item in obj)
        {
            hash.Add(item, StringComparer.OrdinalIgnoreCase);
        }

        return hash.ToHashCode();
    }
}

