namespace InSpectra.Discovery.Tool.Help.Inference.Text;

using System.Text.RegularExpressions;

internal static partial class TextNoiseClassifier
{
    public static bool HasContentSections(IReadOnlyDictionary<string, List<string>> sections)
        => sections.Any(pair => pair.Value.Count > 0 || string.Equals(pair.Key, "commands", StringComparison.OrdinalIgnoreCase));

    public static bool ShouldIgnorePreambleLine(string line)
    {
        var trimmed = line.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? false
            : LooksLikeDecorativeBannerLine(trimmed)
                || LooksLikeMarketingTagline(trimmed)
                || IsFrameworkNoiseLine(trimmed)
                || IsStructuredLogLine(trimmed)
                || LooksLikeSetupPreambleLine(trimmed)
                || LooksLikeInventoryHeaderLine(trimmed)
                || trimmed.StartsWith("Visit http://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Visit https://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("NSwag bin directory:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("CLI Version:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldRejectHelpCapture(
        IReadOnlyList<string> preamble,
        IReadOnlyDictionary<string, List<string>> sections,
        string? commandHeader,
        string line)
    {
        if (!LooksLikeRejectedHelpInvocation(line.Trim()))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(commandHeader) || HasContentSections(sections))
        {
            return false;
        }

        var meaningfulPreambleLineCount = preamble
            .Select(entry => entry.Trim())
            .Count(entry => !string.IsNullOrWhiteSpace(entry) && !ShouldIgnorePreambleLine(entry));
        return meaningfulPreambleLineCount <= 1;
    }

    public static bool IsArgumentNoiseLine(string line)
        => line.StartsWith("Press <enter>", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Duration:", StringComparison.OrdinalIgnoreCase);

    public static bool IsFrameworkNoiseLine(string line)
        => string.Equals(line, "Error parsing", StringComparison.OrdinalIgnoreCase)
            || CommandLineErrorTokenRegex().IsMatch(line)
            || LooksLikeRejectedHelpInvocation(line);

    public static bool LooksLikeSubcommandHelpHint(string line)
        => line.StartsWith("Use '", StringComparison.OrdinalIgnoreCase)
            && line.Contains("--help", StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeInventoryHeaderLine(string line)
        => DotnetToolListHeaderRegex().IsMatch(line)
            || TemplateInstallHeaderRegex().IsMatch(line);

    public static bool LooksLikeRejectedHelpInvocation(string? firstLine, string? secondLine)
    {
        if (LooksLikeRejectedHelpInvocation(firstLine))
        {
            return true;
        }

        return string.Equals(firstLine?.Trim(), "ERROR(S):", StringComparison.OrdinalIgnoreCase)
            && LooksLikeRejectedHelpInvocation(secondLine);
    }

    private static bool IsStructuredLogLine(string line)
        => StructuredLogPrefixRegex().IsMatch(line);

    private static bool LooksLikeSetupPreambleLine(string line)
        => line.StartsWith("No parameters file found", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Creating a template parameters file", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Parameters file created:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Please edit this file with your", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeRejectedHelpInvocation(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return RejectedHelpInvocationRegex().IsMatch(line.Trim());
    }

    private static bool LooksLikeDecorativeBannerLine(string line)
        => line.Length > 0
            && line.Any(ch => !char.IsWhiteSpace(ch))
            && !line.Any(char.IsLetterOrDigit);

    private static bool LooksLikeMarketingTagline(string line)
        => line.StartsWith("Made with ", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Contact:", StringComparison.OrdinalIgnoreCase)
            || (line.StartsWith("for ", StringComparison.OrdinalIgnoreCase)
                && line.EndsWith("!", StringComparison.Ordinal));

    [GeneratedRegex(@"^CommandLine\.[A-Za-z]+Error$", RegexOptions.Compiled)]
    private static partial Regex CommandLineErrorTokenRegex();

    [GeneratedRegex(@"^(?:trace|debug|info|warn|warning|fail|error):\s|^\[\d{2}:\d{2}:\d{2}\s+[A-Z]{3}\]\s", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex StructuredLogPrefixRegex();

    [GeneratedRegex(@"^(?:--help|-h|-\?|/\?)\s+is an unknown (?:parameter|option|argument)\b|^Invalid usage\b|^Unknown argument or flag for value --help\b|^(?:unknown|unrecognized)\s+(?:option|parameter|argument)\b.*(?:--help|-h|-\?|/\?)\b|^(?:unknown|unrecognized)\s+command\b.*\bhelp\b|^usage error\b.*(?:--help|-h|-\?|/\?)\b|^error\(\d+\):\s+unknown command-line option\s+(?:--help|-h|-\?|/\?)\b|^Verb\s+'(?:--help|-h|-\?|/\?)'\s+is not recognized\.$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex RejectedHelpInvocationRegex();

    [GeneratedRegex(@"^Package Id\s{2,}Version\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex DotnetToolListHeaderRegex();

    [GeneratedRegex(@"^Template Name\s{2,}Short Name\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TemplateInstallHeaderRegex();
}

