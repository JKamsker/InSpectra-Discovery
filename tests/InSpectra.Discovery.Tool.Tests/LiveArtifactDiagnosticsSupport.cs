namespace InSpectra.Discovery.Tool.Tests;

using System.Text;

internal static class LiveArtifactDiagnosticsSupport
{
    public static async Task<string> BuildMissingArtifactMessageAsync(
        string packageId,
        string outputRoot,
        string resultPath,
        string openCliPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Missing OpenCLI artifact for {packageId}.");
        builder.AppendLine($"Output root: {outputRoot}");
        builder.AppendLine($"result.json exists: {File.Exists(resultPath)}");
        builder.AppendLine($"opencli.json exists: {File.Exists(openCliPath)}");

        if (Directory.Exists(outputRoot))
        {
            builder.AppendLine("Output files:");

            foreach (var filePath in Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories).OrderBy(static path => path, StringComparer.Ordinal))
            {
                var fileInfo = new FileInfo(filePath);
                builder.AppendLine($"- {Path.GetRelativePath(outputRoot, filePath)} ({fileInfo.Length} bytes)");
            }
        }

        if (File.Exists(resultPath))
        {
            builder.AppendLine("result.json:");
            builder.AppendLine(await File.ReadAllTextAsync(resultPath));
        }

        return builder.ToString();
    }
}
