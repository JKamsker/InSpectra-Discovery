namespace InSpectra.Discovery.Tool.Infrastructure.Commands;

using System.Text.RegularExpressions;

internal static partial class DotnetRuntimeCompatibilitySupport
{
    public const string GlobalizationInvariantEnvironmentVariableName = "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT";
    public const string DotnetRollForwardEnvironmentVariableName = "DOTNET_ROLL_FORWARD";
    public const string DotnetRollForwardMajorValue = "Major";
    private const string MissingFrameworkMessage = "You must install or update .NET to run this application.";

    public static async Task<CommandRuntime.ProcessResult> InvokeWithCompatibilityRetriesAsync(
        CommandRuntime runtime,
        string filePath,
        IReadOnlyList<string> argumentList,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        string? sandboxRoot,
        CancellationToken cancellationToken)
    {
        var effectiveEnvironment = environment;
        var processResult = await runtime.InvokeProcessCaptureAsync(
            filePath,
            argumentList,
            workingDirectory,
            effectiveEnvironment,
            timeoutSeconds,
            sandboxRoot,
            cancellationToken);

        if (LooksLikeMissingIcu(processResult)
            && !effectiveEnvironment.ContainsKey(GlobalizationInvariantEnvironmentVariableName))
        {
            effectiveEnvironment = new Dictionary<string, string>(effectiveEnvironment, StringComparer.OrdinalIgnoreCase)
            {
                [GlobalizationInvariantEnvironmentVariableName] = "1",
            };
            processResult = await runtime.InvokeProcessCaptureAsync(
                filePath,
                argumentList,
                workingDirectory,
                effectiveEnvironment,
                timeoutSeconds,
                sandboxRoot,
                cancellationToken);
        }

        if (LooksLikeMissingSharedRuntime(processResult)
            && !effectiveEnvironment.ContainsKey(DotnetRollForwardEnvironmentVariableName))
        {
            effectiveEnvironment = new Dictionary<string, string>(effectiveEnvironment, StringComparer.OrdinalIgnoreCase)
            {
                [DotnetRollForwardEnvironmentVariableName] = DotnetRollForwardMajorValue,
            };
            processResult = await runtime.InvokeProcessCaptureAsync(
                filePath,
                argumentList,
                workingDirectory,
                effectiveEnvironment,
                timeoutSeconds,
                sandboxRoot,
                cancellationToken);
        }

        return processResult;
    }

    public static DotnetRuntimeIssue? DetectMissingFramework(string? command, string? stdout, string? stderr)
    {
        var message = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
        if (string.IsNullOrWhiteSpace(message)
            || !message.Contains(MissingFrameworkMessage, StringComparison.Ordinal))
        {
            return null;
        }

        var match = RequiredFrameworkRegex().Match(message);
        var requirement = match.Success
            ? new DotnetRuntimeRequirement(
                match.Groups["name"].Value,
                match.Groups["version"].Value)
            : null;

        return new DotnetRuntimeIssue(
            ToDisplayCommand(command),
            Mode: "missing-framework",
            Requirement: requirement);
    }

    public static string BuildMissingFrameworkFailureMessage(
        IReadOnlyList<string> blockedCommands,
        IReadOnlyList<DotnetRuntimeRequirement> requiredFrameworks)
    {
        var commands = blockedCommands
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var frameworks = requiredFrameworks
            .Distinct()
            .Select(requirement => $"{requirement.Name} {requirement.Version}")
            .ToArray();

        var commandText = commands.Length == 0
            ? "<root>"
            : string.Join(", ", commands);
        var frameworkText = frameworks.Length == 0
            ? "The required framework version could not be parsed from the tool output."
            : $"Required frameworks: {string.Join(", ", frameworks)}.";

        return $"The installed tool could not be executed because the host is missing a compatible .NET shared framework even after retrying with {DotnetRollForwardEnvironmentVariableName}={DotnetRollForwardMajorValue}. Blocked commands: {commandText}. {frameworkText}";
    }

    public static bool LooksLikeMissingIcu(CommandRuntime.ProcessResult processResult)
        => LooksLikeMissingIcu(processResult.Stdout, processResult.Stderr);

    public static bool LooksLikeMissingSharedRuntime(CommandRuntime.ProcessResult processResult)
        => LooksLikeMissingSharedRuntime(processResult.Stdout, processResult.Stderr);

    public static string ToDisplayCommand(string? command)
        => string.IsNullOrWhiteSpace(command) ? "<root>" : command;

    private static bool LooksLikeMissingIcu(string? stdout, string? stderr)
    {
        var combined = BuildCombinedText(stdout, stderr);
        return !string.IsNullOrWhiteSpace(combined)
            && (combined.Contains("Couldn't find a valid ICU package installed on the system", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("System.Globalization.Invariant", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("libicu", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeMissingSharedRuntime(string? stdout, string? stderr)
    {
        var combined = BuildCombinedText(stdout, stderr);
        return !string.IsNullOrWhiteSpace(combined)
            && (combined.Contains(MissingFrameworkMessage, StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Framework: 'Microsoft.NETCore.App'", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("The following frameworks were found:", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildCombinedText(string? stdout, string? stderr)
        => string.Join(
            "\n",
            SplitLines(stderr)
                .Concat(SplitLines(stdout))
                .Where(line => !string.IsNullOrWhiteSpace(line)));

    private static IEnumerable<string?> SplitLines(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    [GeneratedRegex(@"Framework:\s*'(?<name>[^']+)',\s*version\s*'(?<version>[^']+)'", RegexOptions.Compiled)]
    private static partial Regex RequiredFrameworkRegex();
}

internal sealed record DotnetRuntimeIssue(
    string Command,
    string Mode,
    DotnetRuntimeRequirement? Requirement);

internal sealed record DotnetRuntimeRequirement(
    string Name,
    string Version);
