using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;

internal sealed class CliFxPackageArchiveInspector
{
    private readonly NuGetApiClient _apiClient;

    public CliFxPackageArchiveInspector(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<CliFxPackageInspection> InspectAsync(string packageContentUrl, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"inspectra-clifx-{Guid.NewGuid():N}.nupkg");

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

    private static CliFxPackageInspection InspectArchive(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);

        var depsFilePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var dependencyVersions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var assemblies = new List<CliFxAssemblyVersionInfo>();
        var toolSettingsPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolCommandNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolEntryPointPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolDirectories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolAssembliesReferencingCliFx = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))
            {
                depsFilePaths.Add(entry.FullName);
                ReadDependencyVersions(entry, dependencyVersions);
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

            if (string.Equals(entry.Name, "CliFx.dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblies.Add(ReadAssemblyVersionInfo(entry));
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

                if (ReadAssemblyReferences(entry))
                {
                    toolAssembliesReferencingCliFx.Add(entry.FullName);
                }
            }
        }

        return new CliFxPackageInspection(
            DepsFilePaths: depsFilePaths.ToArray(),
            CliFxDependencyVersions: dependencyVersions.ToArray(),
            CliFxAssemblies: assemblies.OrderBy(assembly => assembly.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            ToolSettingsPaths: toolSettingsPaths.ToArray(),
            ToolCommandNames: toolCommandNames.ToArray(),
            ToolEntryPointPaths: toolEntryPointPaths.ToArray(),
            ToolAssembliesReferencingCliFx: toolAssembliesReferencingCliFx.ToArray());
    }

    private static void ReadDependencyVersions(ZipArchiveEntry entry, ISet<string> dependencyVersions)
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
            if (TryParsePackageVersion(library.Name, "CliFx", out var version))
            {
                dependencyVersions.Add(version);
            }
        }
    }

    private static CliFxAssemblyVersionInfo ReadAssemblyVersionInfo(ZipArchiveEntry entry)
    {
        using var sourceStream = entry.Open();
        using var memoryStream = new MemoryStream();
        sourceStream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        using var peReader = new PEReader(memoryStream, PEStreamOptions.LeaveOpen);
        if (!peReader.HasMetadata)
        {
            return new CliFxAssemblyVersionInfo(entry.FullName, null, null, null);
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

        return new CliFxAssemblyVersionInfo(entry.FullName, assemblyDefinition.Version.ToString(), fileVersion, informationalVersion);
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
                GetAttributeValue(element, "Name") ?? fallbackCommandName,
                NormalizeArchivePath(toolDirectory, GetAttributeValue(element, "EntryPoint") ?? fallbackEntryPoint)))
            .Where(command => !string.IsNullOrWhiteSpace(command.CommandName) || !string.IsNullOrWhiteSpace(command.EntryPointPath))
            .ToArray();

        if (commands.Length > 0)
        {
            return commands;
        }

        return string.IsNullOrWhiteSpace(fallbackCommandName) && string.IsNullOrWhiteSpace(fallbackEntryPoint)
            ? []
            : [new ToolCommandDescriptor(fallbackCommandName, NormalizeArchivePath(toolDirectory, fallbackEntryPoint))];
    }

    private static bool ReadAssemblyReferences(ZipArchiveEntry entry)
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
                return false;
            }

            var reader = peReader.GetMetadataReader();
            return reader.AssemblyReferences.Any(handle =>
                string.Equals(reader.GetString(reader.GetAssemblyReference(handle).Name), "CliFx", StringComparison.OrdinalIgnoreCase));
        }
        catch (BadImageFormatException)
        {
            return false;
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
            return valueReader.Length >= 2 && valueReader.ReadUInt16() == 1 ? valueReader.ReadSerializedString() : null;
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
            HandleKind.TypeReference => GetQualifiedName(reader, reader.GetTypeReference((TypeReferenceHandle)handle)),
            HandleKind.TypeDefinition => GetQualifiedName(reader, reader.GetTypeDefinition((TypeDefinitionHandle)handle)),
            _ => null,
        };

    private static string GetQualifiedName(MetadataReader reader, TypeReference type)
        => GetQualifiedName(reader.GetString(type.Namespace), reader.GetString(type.Name));

    private static string GetQualifiedName(MetadataReader reader, TypeDefinition type)
        => GetQualifiedName(reader.GetString(type.Namespace), reader.GetString(type.Name));

    private static string GetQualifiedName(string? typeNamespace, string? typeName)
        => string.IsNullOrEmpty(typeNamespace) ? typeName ?? string.Empty : $"{typeNamespace}.{typeName}";

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
        => container.Descendants()
            .FirstOrDefault(element => localNames.Any(name => string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)))
            ?.Value
            .Trim();

    private static bool IsToolManagedAssembly(ZipArchiveEntry entry, IReadOnlySet<string> toolDirectories)
    {
        if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            && !entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(entry.Name, "CliFx.dll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return toolDirectories.Any(directory =>
            entry.FullName.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.FullName, directory, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record ToolCommandDescriptor(string? CommandName, string? EntryPointPath);
}
