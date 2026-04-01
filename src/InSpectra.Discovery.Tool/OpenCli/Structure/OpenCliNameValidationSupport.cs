namespace InSpectra.Discovery.Tool.OpenCli.Structure;

using System.Text.RegularExpressions;

internal static partial class OpenCliNameValidationSupport
{
    public static bool TryValidateCommandName(string? name, string path, out string? reason)
        => TryValidateName(name, path, "command", LooksLikeNonPublishableCommandName, out reason);

    public static bool TryValidateArgumentName(string? name, string path, out string? reason)
        => TryValidateName(name, path, "argument", LooksLikeNonPublishableArgumentName, out reason);

    private static bool TryValidateName(
        string? name,
        string path,
        string kind,
        Func<string, bool> isNonPublishable,
        out string? reason)
    {
        reason = null;

        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return true;
        }

        if (!isNonPublishable(trimmed))
        {
            return true;
        }

        reason = $"OpenCLI artifact has a non-publishable {kind} name '{trimmed}' at '{path}'.";
        return false;
    }

    private static bool LooksLikeNonPublishableCommandName(string name)
        => PlaceholderCommandNameRegex().IsMatch(name)
            || ObfuscatedNameRegex().IsMatch(name)
            || EnvironmentAssignmentSnippetRegex().IsMatch(name);

    private static bool LooksLikeNonPublishableArgumentName(string name)
        => ObfuscatedNameRegex().IsMatch(name)
            || EnvironmentAssignmentSnippetRegex().IsMatch(name)
            || HeadingLabelRegex().IsMatch(name)
            || OptionSyntaxRegex().IsMatch(name)
            || UppercaseSentenceLabelRegex().IsMatch(name);

    [GeneratedRegex(@"^\.\.?$", RegexOptions.Compiled)]
    private static partial Regex PlaceholderCommandNameRegex();

    [GeneratedRegex(@"^#=[A-Za-z0-9$+/]+=$", RegexOptions.Compiled)]
    private static partial Regex ObfuscatedNameRegex();

    [GeneratedRegex(@"^[""']?[A-Z][A-Z0-9_]*=[^""'\s]+[""']?\]?$", RegexOptions.Compiled)]
    private static partial Regex EnvironmentAssignmentSnippetRegex();

    [GeneratedRegex(@"^[A-Z][A-Z0-9 /_-]*:$", RegexOptions.Compiled)]
    private static partial Regex HeadingLabelRegex();

    [GeneratedRegex(@"^(?:-{1,2}|/)\S*(?:,\s*(?:-{1,2}|/)\S+)?(?:\s+<[^>]+>?|\.\.\.)?$", RegexOptions.Compiled)]
    private static partial Regex OptionSyntaxRegex();

    [GeneratedRegex(@"^(?:[A-Z][A-Z0-9]*)(?: [A-Z][A-Z0-9]*){2,}$", RegexOptions.Compiled)]
    private static partial Regex UppercaseSentenceLabelRegex();
}
