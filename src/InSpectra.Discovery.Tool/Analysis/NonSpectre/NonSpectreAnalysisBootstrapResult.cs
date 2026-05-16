namespace InSpectra.Discovery.Tool.Analysis.NonSpectre;


internal sealed record NonSpectreAnalysisBootstrapResult(
    string PackageContentUrl,
    ResolvedToolCommandInfo CommandInfo)
{
    public string? CommandName => CommandInfo.CommandName;

    public string? EntryPointPath => CommandInfo.EntryPointPath;

    public string? ToolSettingsPath => CommandInfo.ToolSettingsPath;
}
