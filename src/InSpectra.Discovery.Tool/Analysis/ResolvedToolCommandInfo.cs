namespace InSpectra.Discovery.Tool.Analysis;

internal sealed record ResolvedToolCommandInfo(
    string? CommandName,
    string? EntryPointPath,
    string? ToolSettingsPath);
