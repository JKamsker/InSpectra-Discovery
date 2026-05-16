namespace InSpectra.Discovery.Tool.Packages;


using System.Xml.Linq;

internal static class DotnetToolSettingsReader
{
    public static DotnetToolSettingsDocument Read(Stream stream, string settingsPath)
    {
        var document = XDocument.Load(stream);
        var toolDirectory = PackageArchivePathSupport.GetArchiveDirectory(settingsPath);
        var fallbackCommandName = GetFirstDescendantValue(document, "ToolCommandName", "CommandName");
        var fallbackEntryPoint = GetFirstDescendantValue(document, "EntryPoint");

        var commands = document
            .Descendants()
            .Where(element => HasName(element, "Command"))
            .Select(element => new DotnetToolSettingsCommand(
                CommandName: GetAttributeValue(element, "Name") ?? fallbackCommandName,
                EntryPointPath: PackageArchivePathSupport.NormalizeArchivePath(
                    toolDirectory,
                    GetAttributeValue(element, "EntryPoint") ?? fallbackEntryPoint)))
            .Where(command => !string.IsNullOrWhiteSpace(command.CommandName) || !string.IsNullOrWhiteSpace(command.EntryPointPath))
            .ToArray();

        if (commands.Length == 0
            && (!string.IsNullOrWhiteSpace(fallbackCommandName) || !string.IsNullOrWhiteSpace(fallbackEntryPoint)))
        {
            commands =
            [
                new DotnetToolSettingsCommand(
                    fallbackCommandName,
                    PackageArchivePathSupport.NormalizeArchivePath(toolDirectory, fallbackEntryPoint)),
            ];
        }

        return new DotnetToolSettingsDocument(toolDirectory, commands);
    }

    private static string? GetAttributeValue(XElement element, string name)
        => element.Attributes().FirstOrDefault(attribute => HasName(attribute, name))?.Value;

    private static string? GetFirstDescendantValue(XContainer container, params string[] localNames)
    {
        var match = container.Descendants().FirstOrDefault(element => localNames.Any(name => HasName(element, name)));
        return match?.Value.Trim();
    }

    private static bool HasName(XElement element, string localName)
        => string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);

    private static bool HasName(XAttribute attribute, string localName)
        => string.Equals(attribute.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);
}

internal sealed record DotnetToolSettingsDocument(
    string ToolDirectory,
    IReadOnlyList<DotnetToolSettingsCommand> Commands);

internal sealed record DotnetToolSettingsCommand(
    string? CommandName,
    string? EntryPointPath);

