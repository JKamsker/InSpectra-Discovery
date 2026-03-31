namespace InSpectra.Discovery.Tool.Analysis.Hook;

using InSpectra.Discovery.Tool.Infrastructure.Commands;

using System.Text;

internal static class HookFailureMessageSupport
{
    private const int MaxDiagnosticLength = 600;

    public static string BuildMissingCaptureMessage(CommandRuntime.ProcessResult processResult)
    {
        var builder = new StringBuilder("Startup hook did not produce a capture file.");

        if (processResult.TimedOut)
        {
            builder.Append(" The tool command timed out.");
        }
        else if (processResult.ExitCode.HasValue)
        {
            builder.Append($" Exit code: {processResult.ExitCode.Value}.");
        }

        AppendDiagnostic(builder, "stderr", processResult.Stderr);
        AppendDiagnostic(builder, "stdout", processResult.Stdout);
        return builder.ToString();
    }

    private static void AppendDiagnostic(StringBuilder builder, string label, string? value)
    {
        var normalized = CommandRuntime.NormalizeConsoleText(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        builder.Append(' ');
        builder.Append(label);
        builder.Append(": ");
        builder.Append(Truncate(normalized, MaxDiagnosticLength));
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";
}
