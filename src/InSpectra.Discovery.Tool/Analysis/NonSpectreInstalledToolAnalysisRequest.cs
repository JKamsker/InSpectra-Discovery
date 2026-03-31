namespace InSpectra.Discovery.Tool.Analysis;

using System.Text.Json.Nodes;

internal sealed record NonSpectreInstalledToolAnalysisRequest(
    JsonObject Result,
    string PackageId,
    string Version,
    string CommandName,
    string? CliFramework,
    string OutputDirectory,
    string TempRoot,
    int InstallTimeoutSeconds,
    int CommandTimeoutSeconds);
