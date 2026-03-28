using System.Text.RegularExpressions;

internal static class AnalysisIntrospectionFailureClassifier
{
    public static string? Classify(IReadOnlyList<string> argumentList, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escapedSegments = argumentList.Select(Regex.Escape).ToArray();
        var subcommandPattern = escapedSegments.Length > 0
            ? $"(?:{string.Join("|", escapedSegments)})"
            : "(?:cli|opencli|xmldoc)";

        if (MatchesAny(text, [
            $@"\bunknown command\b.*\b{subcommandPattern}\b",
            $@"\bunrecognized command\b.*\b{subcommandPattern}\b",
            $@"\bunknown argument\b.*\b{subcommandPattern}\b",
            $@"\bunrecognized argument\b.*\b{subcommandPattern}\b",
            $@"\b{subcommandPattern}\b.*\b(?:not recognized|not found|not a valid command|invalid command)\b",
            $@"\bcould not match\b.*\b{subcommandPattern}\b",
            @"\bcould not resolve type\b.*\b(?:opencli|xmldoc|spectre\.console\.cli\.(?:opendoc|xmldoc|xml?doc)command|spectre\.console\.cli\.xmldoccommand)\b",
            @"\brequired command was not provided\b",
        ]))
        {
            return "unsupported-command";
        }

        if (MatchesAny(text, [
            @"\byou must install or update \.net\b",
            @"\bframework:\s+'?microsoft\.netcore\.app",
            @"\bno frameworks? were found\b",
            @"\bthe following frameworks were found\b",
        ]))
        {
            return "environment-missing-runtime";
        }

        if (MatchesAny(text, [
            @"\b(?:unable to load shared library|cannot open shared object file|dllnotfoundexception|could not load file or assembly|libsecret)\b",
        ]))
        {
            return "environment-missing-dependency";
        }

        if (MatchesAny(text, [
            @"\b(?:current terminal isn't interactive|non-interactive mode|cannot prompt|cannot show selection prompt|failed to read input in non-interactive mode)\b",
        ]))
        {
            return "requires-interactive-input";
        }

        if (MatchesAny(text, [
            @"\b(?:windows only|unsupported operating system|platform not supported|os platform is not supported)\b",
        ]))
        {
            return "unsupported-platform";
        }

        if (MatchesAny(text, [
            @"\b(?:checking your credentials|credential|authenticate|authentication|device code|sign in|login|log in|open (?:the )? browser)\b",
        ]))
        {
            return "requires-interactive-authentication";
        }

        if (MatchesAny(text, [
            @"\b(?:no|missing)\b.*\b(?:config|configuration)\b",
            @"\bconfiguration\b",
            @"\b(?:required option|required argument)\b",
            @"\bmust be specified\b",
            @"\bnot enough arguments\b",
        ]))
        {
            return "requires-configuration";
        }

        return null;
    }

    private static bool MatchesAny(string text, IEnumerable<string> patterns)
        => patterns.Any(pattern => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline));
}
