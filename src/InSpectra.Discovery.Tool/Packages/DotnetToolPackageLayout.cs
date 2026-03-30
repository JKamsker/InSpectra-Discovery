internal sealed record DotnetToolPackageLayout(
    IReadOnlyList<string> ToolSettingsPaths,
    IReadOnlyList<string> ToolCommandNames,
    IReadOnlyList<string> ToolEntryPointPaths,
    IReadOnlySet<string> ToolDirectories);

internal sealed class DotnetToolPackageLayoutBuilder
{
    private readonly SortedSet<string> _toolSettingsPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedSet<string> _toolCommandNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedSet<string> _toolEntryPointPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedSet<string> _toolDirectories = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string settingsPath, DotnetToolSettingsDocument document)
    {
        _toolSettingsPaths.Add(settingsPath);
        if (!string.IsNullOrWhiteSpace(document.ToolDirectory))
        {
            _toolDirectories.Add(document.ToolDirectory);
        }

        foreach (var command in document.Commands)
        {
            if (!string.IsNullOrWhiteSpace(command.CommandName))
            {
                _toolCommandNames.Add(command.CommandName);
            }

            if (!string.IsNullOrWhiteSpace(command.EntryPointPath))
            {
                _toolEntryPointPaths.Add(command.EntryPointPath);
            }
        }
    }

    public DotnetToolPackageLayout Build()
        => new(
            _toolSettingsPaths.ToArray(),
            _toolCommandNames.ToArray(),
            _toolEntryPointPaths.ToArray(),
            new SortedSet<string>(_toolDirectories, StringComparer.OrdinalIgnoreCase));
}
