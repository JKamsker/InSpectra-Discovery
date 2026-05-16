namespace InSpectra.Discovery.Tool.Docs.Reports;

internal static class DocsDocumentationReportStabilitySupport
{
    private const string GeneratedPrefix = "Generated: ";

    public static IReadOnlyList<string> PreserveGeneratedLineWhenContentIsUnchanged(
        string outputPath,
        IReadOnlyList<string> candidateLines)
    {
        if (!File.Exists(outputPath))
        {
            return candidateLines;
        }

        var existingLines = File.ReadAllLines(outputPath);
        if (existingLines.Length != candidateLines.Count)
        {
            return candidateLines;
        }

        for (var index = 0; index < candidateLines.Count; index++)
        {
            var candidateLine = candidateLines[index];
            var existingLine = existingLines[index];
            if (IsGeneratedLine(candidateLine) && IsGeneratedLine(existingLine))
            {
                continue;
            }

            if (!string.Equals(candidateLine, existingLine, StringComparison.Ordinal))
            {
                return candidateLines;
            }
        }

        var stabilizedLines = candidateLines.ToArray();
        for (var index = 0; index < stabilizedLines.Length; index++)
        {
            if (IsGeneratedLine(stabilizedLines[index]) && IsGeneratedLine(existingLines[index]))
            {
                stabilizedLines[index] = existingLines[index];
            }
        }

        return stabilizedLines;
    }

    private static bool IsGeneratedLine(string line)
        => line.StartsWith(GeneratedPrefix, StringComparison.Ordinal);
}
