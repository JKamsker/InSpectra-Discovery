namespace InSpectra.Discovery.Tool.Analysis.NonSpectre;


internal sealed record ResolvedToolCommandInfo(
    string? CommandName,
    string? EntryPointPath,
    string? ToolSettingsPath);
