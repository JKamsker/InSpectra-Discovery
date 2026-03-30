using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

internal static class PackageArchivePortableExecutableSupport
{
    public static PackageArchiveAssemblyInspection ReadAssemblyInspection(ZipArchiveEntry entry)
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
                return PackageArchiveAssemblyInspection.Empty(entry.FullName);
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

            var assemblyReferences = reader.AssemblyReferences
                .Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new PackageArchiveAssemblyInspection(
                entry.FullName,
                assemblyDefinition.Version.ToString(),
                fileVersion,
                informationalVersion,
                assemblyReferences);
        }
        catch (BadImageFormatException)
        {
            return PackageArchiveAssemblyInspection.Empty(entry.FullName);
        }
    }

    public static bool HasReference(PackageArchiveAssemblyInspection inspection, string assemblyName)
        => inspection.AssemblyReferences.Any(name => string.Equals(name, assemblyName, StringComparison.OrdinalIgnoreCase));

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
}

internal sealed record PackageArchiveAssemblyInspection(
    string Path,
    string? AssemblyVersion,
    string? FileVersion,
    string? InformationalVersion,
    IReadOnlyList<string> AssemblyReferences)
{
    public static PackageArchiveAssemblyInspection Empty(string path) => new(path, null, null, null, []);
}
