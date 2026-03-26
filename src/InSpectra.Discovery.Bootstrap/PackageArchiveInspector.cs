using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;

internal sealed class PackageArchiveInspector
{
    private readonly NuGetApiClient _apiClient;

    public PackageArchiveInspector(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<SpectrePackageInspection> InspectAsync(string packageContentUrl, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"inspectra-{Guid.NewGuid():N}.nupkg");

        try
        {
            await _apiClient.DownloadFileAsync(packageContentUrl, tempPath, cancellationToken);
            return InspectArchive(tempPath);
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
            }
        }
    }

    private static SpectrePackageInspection InspectArchive(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);

        var depsFilePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var spectreConsoleDependencyVersions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var spectreConsoleCliDependencyVersions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var spectreConsoleAssemblies = new List<SpectreAssemblyVersionInfo>();
        var spectreConsoleCliAssemblies = new List<SpectreAssemblyVersionInfo>();
        var toolSettingsPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolCommandNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolEntryPointPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolDirectories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolAssembliesReferencingSpectreConsole = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolAssembliesReferencingSpectreConsoleCli = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))
            {
                depsFilePaths.Add(entry.FullName);
                ReadDependencyVersions(entry, spectreConsoleDependencyVersions, spectreConsoleCliDependencyVersions);
                continue;
            }

            if (string.Equals(entry.Name, "DotnetToolSettings.xml", StringComparison.OrdinalIgnoreCase))
            {
                toolSettingsPaths.Add(entry.FullName);
                var toolDirectory = GetArchiveDirectory(entry.FullName);
                if (!string.IsNullOrWhiteSpace(toolDirectory))
                {
                    toolDirectories.Add(toolDirectory);
                }

                foreach (var command in ReadToolCommands(entry))
                {
                    if (!string.IsNullOrWhiteSpace(command.CommandName))
                    {
                        toolCommandNames.Add(command.CommandName);
                    }

                    if (!string.IsNullOrWhiteSpace(command.EntryPointPath))
                    {
                        toolEntryPointPaths.Add(command.EntryPointPath);
                    }
                }

                continue;
            }

            if (string.Equals(entry.Name, "Spectre.Console.Cli.dll", StringComparison.OrdinalIgnoreCase))
            {
                spectreConsoleCliAssemblies.Add(ReadAssemblyVersionInfo(entry));
                continue;
            }

            if (string.Equals(entry.Name, "Spectre.Console.dll", StringComparison.OrdinalIgnoreCase))
            {
                spectreConsoleAssemblies.Add(ReadAssemblyVersionInfo(entry));
            }
        }

        if (toolDirectories.Count > 0)
        {
            foreach (var entry in archive.Entries)
            {
                if (!IsToolManagedAssembly(entry, toolDirectories))
                {
                    continue;
                }

                var references = ReadAssemblyReferences(entry);
                if (references.HasSpectreConsole)
                {
                    toolAssembliesReferencingSpectreConsole.Add(entry.FullName);
                }

                if (references.HasSpectreConsoleCli)
                {
                    toolAssembliesReferencingSpectreConsoleCli.Add(entry.FullName);
                }
            }
        }

        return new SpectrePackageInspection(
            DepsFilePaths: depsFilePaths.ToArray(),
            SpectreConsoleDependencyVersions: spectreConsoleDependencyVersions.ToArray(),
            SpectreConsoleCliDependencyVersions: spectreConsoleCliDependencyVersions.ToArray(),
            SpectreConsoleAssemblies: spectreConsoleAssemblies
                .OrderBy(assembly => assembly.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SpectreConsoleCliAssemblies: spectreConsoleCliAssemblies
                .OrderBy(assembly => assembly.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ToolSettingsPaths: toolSettingsPaths.ToArray(),
            ToolCommandNames: toolCommandNames.ToArray(),
            ToolEntryPointPaths: toolEntryPointPaths.ToArray(),
            ToolAssembliesReferencingSpectreConsole: toolAssembliesReferencingSpectreConsole.ToArray(),
            ToolAssembliesReferencingSpectreConsoleCli: toolAssembliesReferencingSpectreConsoleCli.ToArray());
    }

    private static void ReadDependencyVersions(
        ZipArchiveEntry entry,
        ISet<string> spectreConsoleDependencyVersions,
        ISet<string> spectreConsoleCliDependencyVersions)
    {
        using var stream = entry.Open();
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("libraries", out var libraries)
            || libraries.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var library in libraries.EnumerateObject())
        {
            if (TryParsePackageVersion(library.Name, "Spectre.Console.Cli", out var cliVersion))
            {
                spectreConsoleCliDependencyVersions.Add(cliVersion);
                continue;
            }

            if (TryParsePackageVersion(library.Name, "Spectre.Console", out var consoleVersion))
            {
                spectreConsoleDependencyVersions.Add(consoleVersion);
            }
        }
    }

    private static SpectreAssemblyVersionInfo ReadAssemblyVersionInfo(ZipArchiveEntry entry)
    {
        using var sourceStream = entry.Open();
        using var memoryStream = new MemoryStream();
        sourceStream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        using var peReader = new PEReader(memoryStream, PEStreamOptions.LeaveOpen);
        if (!peReader.HasMetadata)
        {
            return new SpectreAssemblyVersionInfo(entry.FullName, null, null, null);
        }

        var reader = peReader.GetMetadataReader();
        var assemblyDefinition = reader.GetAssemblyDefinition();

        string? fileVersion = null;
        string? informationalVersion = null;

        foreach (var attributeHandle in assemblyDefinition.GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            var attributeTypeName = GetAttributeTypeName(reader, attribute);
            var attributeValue = ReadCustomAttributeString(reader, attribute);

            switch (attributeTypeName)
            {
                case "System.Reflection.AssemblyFileVersionAttribute":
                    fileVersion = attributeValue;
                    break;
                case "System.Reflection.AssemblyInformationalVersionAttribute":
                    informationalVersion = attributeValue;
                    break;
            }
        }

        return new SpectreAssemblyVersionInfo(
            Path: entry.FullName,
            AssemblyVersion: assemblyDefinition.Version.ToString(),
            FileVersion: fileVersion,
            InformationalVersion: informationalVersion);
    }

    private static IReadOnlyList<ToolCommandDescriptor> ReadToolCommands(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        var toolDirectory = GetArchiveDirectory(entry.FullName);
        var fallbackCommandName = GetFirstDescendantValue(document, "ToolCommandName", "CommandName");
        var fallbackEntryPoint = GetFirstDescendantValue(document, "EntryPoint");

        var commands = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "Command", StringComparison.OrdinalIgnoreCase))
            .Select(element => new ToolCommandDescriptor(
                CommandName: GetAttributeValue(element, "Name") ?? fallbackCommandName,
                EntryPointPath: NormalizeArchivePath(toolDirectory, GetAttributeValue(element, "EntryPoint") ?? fallbackEntryPoint)))
            .Where(command => !string.IsNullOrWhiteSpace(command.CommandName) || !string.IsNullOrWhiteSpace(command.EntryPointPath))
            .ToArray();

        if (commands.Length > 0)
        {
            return commands;
        }

        if (string.IsNullOrWhiteSpace(fallbackCommandName) && string.IsNullOrWhiteSpace(fallbackEntryPoint))
        {
            return [];
        }

        return
        [
            new ToolCommandDescriptor(
                CommandName: fallbackCommandName,
                EntryPointPath: NormalizeArchivePath(toolDirectory, fallbackEntryPoint))
        ];
    }

    private static (bool HasSpectreConsole, bool HasSpectreConsoleCli) ReadAssemblyReferences(ZipArchiveEntry entry)
    {
        try
        {
            using var sourceStream = entry.Open();
            using var memoryStream = new MemoryStream();
            sourceStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            using var peReader = new PEReader(memoryStream, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
            {
                return (false, false);
            }

            var reader = peReader.GetMetadataReader();
            var hasSpectreConsole = false;
            var hasSpectreConsoleCli = false;

            foreach (var handle in reader.AssemblyReferences)
            {
                var name = reader.GetString(reader.GetAssemblyReference(handle).Name);
                if (string.Equals(name, "Spectre.Console", StringComparison.OrdinalIgnoreCase))
                {
                    hasSpectreConsole = true;
                }
                else if (string.Equals(name, "Spectre.Console.Cli", StringComparison.OrdinalIgnoreCase))
                {
                    hasSpectreConsoleCli = true;
                }
            }

            return (hasSpectreConsole, hasSpectreConsoleCli);
        }
        catch (BadImageFormatException)
        {
            return (false, false);
        }
    }

    private static bool TryParsePackageVersion(string key, string packageId, out string version)
    {
        var prefix = packageId + "/";
        if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && key.Length > prefix.Length)
        {
            version = key[prefix.Length..];
            return true;
        }

        version = string.Empty;
        return false;
    }

    private static string? ReadCustomAttributeString(MetadataReader reader, CustomAttribute attribute)
    {
        try
        {
            var valueReader = reader.GetBlobReader(attribute.Value);
            if (valueReader.Length < 2 || valueReader.ReadUInt16() != 1)
            {
                return null;
            }

            return valueReader.ReadSerializedString();
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? GetAttributeTypeName(MetadataReader reader, CustomAttribute attribute)
        => attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => GetTypeName(reader, reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent),
            HandleKind.MethodDefinition => GetTypeName(reader, reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType()),
            _ => null,
        };

    private static string? GetTypeName(MetadataReader reader, EntityHandle handle)
        => handle.Kind switch
        {
            HandleKind.TypeReference => GetQualifiedName(reader.GetTypeReference((TypeReferenceHandle)handle).Namespace, reader.GetTypeReference((TypeReferenceHandle)handle).Name, reader),
            HandleKind.TypeDefinition => GetQualifiedName(reader.GetTypeDefinition((TypeDefinitionHandle)handle).Namespace, reader.GetTypeDefinition((TypeDefinitionHandle)handle).Name, reader),
            _ => null,
        };

    private static string GetQualifiedName(StringHandle namespaceHandle, StringHandle nameHandle, MetadataReader reader)
    {
        var typeNamespace = reader.GetString(namespaceHandle);
        var typeName = reader.GetString(nameHandle);
        return string.IsNullOrEmpty(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}";
    }

    private static string GetArchiveDirectory(string path)
    {
        var normalized = path.Replace('\\', '/');
        var index = normalized.LastIndexOf('/');
        return index >= 0 ? normalized[..index] : string.Empty;
    }

    private static string? NormalizeArchivePath(string baseDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            segments.AddRange(baseDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var segment in relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }

                continue;
            }

            segments.Add(segment);
        }

        return string.Join("/", segments);
    }

    private static string? GetAttributeValue(XElement element, string name)
        => element.Attributes().FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? GetFirstDescendantValue(XContainer container, params string[] localNames)
    {
        var match = container
            .Descendants()
            .FirstOrDefault(element => localNames.Any(name => string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)));

        return match is null ? null : match.Value.Trim();
    }

    private static bool IsToolManagedAssembly(ZipArchiveEntry entry, IReadOnlySet<string> toolDirectories)
    {
        if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            && !entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(entry.Name, "Spectre.Console.dll", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.Name, "Spectre.Console.Cli.dll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return toolDirectories.Any(directory =>
            entry.FullName.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.FullName, directory, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record ToolCommandDescriptor(
        string? CommandName,
        string? EntryPointPath);
}
