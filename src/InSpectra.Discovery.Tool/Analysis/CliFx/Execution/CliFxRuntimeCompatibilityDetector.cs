namespace InSpectra.Discovery.Tool.Analysis.CliFx.Execution;

using InSpectra.Discovery.Tool.Analysis.CliFx.Crawling;


using System.Text.RegularExpressions;

internal sealed class CliFxRuntimeCompatibilityDetector
{
    private const string MissingFrameworkMessage = "You must install or update .NET to run this application.";
    private static readonly Regex RequiredFrameworkRegex = new(
        @"Framework:\s*'(?<name>[^']+)',\s*version\s*'(?<version>[^']+)'",
        RegexOptions.Compiled);

    public CliFxRuntimeIssue? Detect(CliFxCaptureSummary capture)
    {
        var message = SelectMessage(capture);
        if (message is null
            || !message.Contains(MissingFrameworkMessage, StringComparison.Ordinal))
        {
            return null;
        }

        var match = RequiredFrameworkRegex.Match(message);
        var requirement = match.Success
            ? new CliFxRuntimeRequirement(match.Groups["name"].Value, match.Groups["version"].Value)
            : null;

        return new CliFxRuntimeIssue(
            Command: ToDisplayCommand(capture.Command),
            Mode: "missing-framework",
            Requirement: requirement);
    }

    public static string ToDisplayCommand(string? command)
        => string.IsNullOrWhiteSpace(command) ? "<root>" : command;

    private static string? SelectMessage(CliFxCaptureSummary capture)
        => !string.IsNullOrWhiteSpace(capture.Stderr)
            ? capture.Stderr
            : capture.Stdout;
}

internal sealed record CliFxRuntimeIssue(
    string Command,
    string Mode,
    CliFxRuntimeRequirement? Requirement);

internal sealed record CliFxRuntimeRequirement(
    string Name,
    string Version);


