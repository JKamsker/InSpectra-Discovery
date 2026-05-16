using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

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

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))
            {
                depsFilePaths.Add(entry.FullName);
                ReadDependencyVersions(entry, spectreConsoleDependencyVersions, spectreConsoleCliDependencyVersions);
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

        return new SpectrePackageInspection(
            DepsFilePaths: depsFilePaths.ToArray(),
            SpectreConsoleDependencyVersions: spectreConsoleDependencyVersions.ToArray(),
            SpectreConsoleCliDependencyVersions: spectreConsoleCliDependencyVersions.ToArray(),
            SpectreConsoleAssemblies: spectreConsoleAssemblies
                .OrderBy(assembly => assembly.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SpectreConsoleCliAssemblies: spectreConsoleCliAssemblies
                .OrderBy(assembly => assembly.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray());
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
}
