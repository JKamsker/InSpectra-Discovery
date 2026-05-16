namespace InSpectra.Discovery.Tool.Infrastructure.Commands;

using System.Xml.Linq;

internal static class InstalledDotnetToolCommandSupport
{
    public static InstalledDotnetToolCommand? TryResolve(string installDirectory, string commandName)
    {
        if (string.IsNullOrWhiteSpace(installDirectory) || string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        foreach (var settingsPath in Directory.EnumerateFiles(installDirectory, "DotnetToolSettings.xml", SearchOption.AllDirectories))
        {
            var command = TryResolveFromSettings(settingsPath, commandName);
            if (command is not null)
            {
                return command;
            }
        }

        return null;
    }

    private static InstalledDotnetToolCommand? TryResolveFromSettings(string settingsPath, string commandName)
    {
        try
        {
            var document = XDocument.Load(settingsPath);
            var commandElement = document
                .Descendants()
                .FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "Command", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        element.Attribute("Name")?.Value,
                        commandName,
                        StringComparison.OrdinalIgnoreCase));
            if (commandElement is null)
            {
                return null;
            }

            var runner = commandElement.Attribute("Runner")?.Value?.Trim();
            var entryPoint = commandElement.Attribute("EntryPoint")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(entryPoint))
            {
                return null;
            }

            var settingsDirectory = Path.GetDirectoryName(settingsPath);
            if (string.IsNullOrWhiteSpace(settingsDirectory))
            {
                return null;
            }

            var entryPointPath = Path.GetFullPath(Path.Combine(
                settingsDirectory,
                entryPoint.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
            return new InstalledDotnetToolCommand(
                commandName,
                runner,
                entryPointPath,
                settingsPath);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record InstalledDotnetToolCommand(
    string CommandName,
    string? Runner,
    string EntryPointPath,
    string SettingsPath);
