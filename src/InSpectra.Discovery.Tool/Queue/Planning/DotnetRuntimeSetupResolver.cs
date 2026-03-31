namespace InSpectra.Discovery.Tool.Queue.Planning;

using InSpectra.Discovery.Tool.Catalog.Filtering.SpectreConsole;
using InSpectra.Discovery.Tool.NuGet;
using InSpectra.Discovery.Tool.Packages;
using InSpectra.Discovery.Tool.Queue.Models;

using System.IO.Compression;
using System.Text.Json.Nodes;

internal static class DotnetRuntimeSetupResolver
{
    private const string BaseSdkChannel = "10.0";

    public static async Task<DotnetSetupPlan> ResolveForPlanItemAsync(
        JsonObject item,
        CatalogLeaf? catalogLeaf,
        string runsOn,
        NuGetApiClient client,
        CancellationToken cancellationToken)
    {
        var precomputed = GetPrecomputed(item);
        if (precomputed is not null)
        {
            return precomputed;
        }

        var catalogTarget = GetCatalogToolTarget(catalogLeaf, runsOn);
        if (catalogTarget is not null)
        {
            if (string.Equals(catalogTarget.Requirement.Channel, BaseSdkChannel, StringComparison.OrdinalIgnoreCase))
            {
                return CreateRuntimeOnlyPlan([catalogTarget.Requirement], "catalog");
            }

            return await ResolveFromArchiveAsync(
                item["packageContentUrl"]?.GetValue<string>(),
                runsOn,
                client,
                cancellationToken);
        }

        return await ResolveFromArchiveAsync(
            item["packageContentUrl"]?.GetValue<string>(),
            runsOn,
            client,
            cancellationToken);
    }

    internal static DotnetSetupPlan? TryResolveFromCatalog(CatalogLeaf? catalogLeaf, string runsOn)
    {
        var target = GetCatalogToolTarget(catalogLeaf, runsOn);
        return target is null
            ? null
            : CreateRuntimeOnlyPlan([target.Requirement], "catalog");
    }

    private static ToolAssetTarget? GetCatalogToolTarget(CatalogLeaf? catalogLeaf, string runsOn)
    {
        if (catalogLeaf?.PackageEntries is null || catalogLeaf.PackageEntries.Count == 0)
        {
            return null;
        }

        return SelectPrimaryToolTarget(
            catalogLeaf.PackageEntries.Select(entry => entry.FullName),
            runsOn);
    }

    internal static ToolAssetTarget? SelectPrimaryToolTarget(IEnumerable<string> entryPaths, string runsOn)
    {
        return entryPaths
            .Select(TryCreateToolAssetTarget)
            .Where(target => target is not null && IsRidCompatible(target.Rid, runsOn))
            .Cast<ToolAssetTarget>()
            .OrderByDescending(target => new Version(target.Requirement.Channel + ".0"))
            .ThenByDescending(target => target.HasPlatformSuffix)
            .ThenByDescending(target => !string.Equals(target.Rid, "any", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    private static async Task<DotnetSetupPlan> ResolveFromArchiveAsync(
        string? packageContentUrl,
        string runsOn,
        NuGetApiClient client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageContentUrl))
        {
            return CreateLegacyFallback("archive-missing-package-content");
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"inspectra-runtime-{Guid.NewGuid():N}.nupkg");
        try
        {
            await client.DownloadFileAsync(packageContentUrl, tempFile, cancellationToken);
            using var archive = ZipFile.OpenRead(tempFile);
            var target = SelectPrimaryToolTarget(archive.Entries.Select(entry => entry.FullName), runsOn);
            if (target is null)
            {
                return CreateLegacyFallback("archive-no-tool-target");
            }

            var runtimeConfigEntries = GetRuntimeConfigEntries(archive, target);

            if (runtimeConfigEntries.Length == 0)
            {
                return CreateRuntimeOnlyPlan([target.Requirement], "archive-target-framework");
            }

            var requirements = new HashSet<DotnetRuntimeRequirement>();
            foreach (var runtimeConfigEntry in runtimeConfigEntries)
            {
                using var reader = new StreamReader(runtimeConfigEntry.Open());
                var document = JsonNode.Parse(await reader.ReadToEndAsync(cancellationToken))?.AsObject();
                if (!TryReadRuntimeRequirements(document, out var parsedRequirements, out var error))
                {
                    return CreateLegacyFallback("archive-runtimeconfig", error);
                }

                foreach (var requirement in parsedRequirements)
                {
                    requirements.Add(requirement);
                }
            }

            if (requirements.Count == 0)
            {
                return CreateRuntimeOnlyPlan([target.Requirement], "archive-target-framework");
            }

            return CreateRuntimeOnlyPlan(requirements.ToArray(), "archive-runtimeconfig");
        }
        catch (Exception ex)
        {
            return CreateLegacyFallback("archive-inspection-failed", ex.Message);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    internal static bool TryReadRuntimeRequirements(
        JsonObject? document,
        out IReadOnlyList<DotnetRuntimeRequirement> requirements,
        out string? error)
        => DotnetRuntimeRequirementReader.TryReadRuntimeRequirements(document, out requirements, out error);

    private static DotnetSetupPlan? GetPrecomputed(JsonObject item)
    {
        var mode = item["dotnetSetupMode"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(mode))
        {
            return null;
        }

        var requirements = item["requiredDotnetRuntimes"]?.AsArray()
            .Select(ToRequirement)
            .Where(requirement => requirement is not null)
            .Cast<DotnetRuntimeRequirement>()
            .ToArray()
            ?? [];

        return new DotnetSetupPlan(
            mode,
            requirements,
            item["dotnetSetupSource"]?.GetValue<string>() ?? "precomputed",
            item["dotnetSetupError"]?.GetValue<string>());
    }

    private static DotnetSetupPlan CreateRuntimeOnlyPlan(
        IReadOnlyList<DotnetRuntimeRequirement> requirements,
        string source)
    {
        var extraRuntimes = requirements
            .Where(requirement => !string.Equals(requirement.Channel, BaseSdkChannel, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .OrderBy(requirement => new Version(requirement.Channel + ".0"))
            .ThenBy(requirement => requirement.Runtime, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DotnetSetupPlan("runtime-only", extraRuntimes, source, Error: null);
    }

    private static DotnetSetupPlan CreateLegacyFallback(string source, string? error = null)
        => new("legacy-multi-sdk", [], source, error);

    private static DotnetRuntimeRequirement? ToRequirement(JsonNode? node)
        => node is not JsonObject value
            ? null
            : new DotnetRuntimeRequirement(
                value["name"]?.GetValue<string>() ?? string.Empty,
                value["version"]?.GetValue<string>() ?? string.Empty,
                value["channel"]?.GetValue<string>() ?? string.Empty,
                value["runtime"]?.GetValue<string>() ?? string.Empty);

    private static ZipArchiveEntry[] GetRuntimeConfigEntries(ZipArchive archive, ToolAssetTarget target)
    {
        var layout = DotnetToolPackageLayoutReader.Read(archive);
        var targetPrefix = target.DirectoryPath + "/";

        var toolRuntimeConfigEntries = layout.ToolEntryPointPaths
            .Where(path => path.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(GetRuntimeConfigPathForEntryPoint)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(archive.GetEntry)
            .Where(entry => entry is not null)
            .Cast<ZipArchiveEntry>()
            .ToArray();
        if (toolRuntimeConfigEntries.Length > 0)
        {
            return toolRuntimeConfigEntries;
        }

        var targetSegmentCount = target.DirectoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
        return archive.Entries
            .Where(entry => IsDirectRuntimeConfigEntry(entry.FullName, targetPrefix, targetSegmentCount))
            .ToArray();
    }

    private static string GetRuntimeConfigPathForEntryPoint(string entryPointPath)
    {
        var normalized = entryPointPath.Replace('\\', '/').Trim('/');
        var lastSlashIndex = normalized.LastIndexOf('/');
        var directoryPath = lastSlashIndex >= 0
            ? normalized[..(lastSlashIndex + 1)]
            : string.Empty;
        var fileName = lastSlashIndex >= 0
            ? normalized[(lastSlashIndex + 1)..]
            : normalized;
        var extensionIndex = fileName.LastIndexOf('.');
        var baseName = extensionIndex > 0
            ? fileName[..extensionIndex]
            : fileName;

        return directoryPath + baseName + ".runtimeconfig.json";
    }

    private static bool IsDirectRuntimeConfigEntry(string entryPath, string targetPrefix, int targetSegmentCount)
    {
        var normalized = entryPath.Replace('\\', '/').Trim('/');
        if (!normalized.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase) ||
            !normalized.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segmentCount = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
        return segmentCount == targetSegmentCount + 1;
    }

    private static ToolAssetTarget? TryCreateToolAssetTarget(string? entryPath)
    {
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            return null;
        }

        var normalized = entryPath.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 4 || !string.Equals(segments[0], "tools", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var requirement = DotnetTargetFrameworkRuntimeSupport.TryResolveRequirement(segments[1]);
        if (requirement is null)
        {
            return null;
        }

        return new ToolAssetTarget(
            DirectoryPath: string.Join('/', segments.Take(3)),
            Rid: segments[2],
            Requirement: requirement,
            HasPlatformSuffix: segments[1].Contains('-', StringComparison.Ordinal));
    }

    private static bool IsRidCompatible(string rid, string runsOn)
    {
        if (string.IsNullOrWhiteSpace(rid) || string.Equals(rid, "any", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return runsOn switch
        {
            "windows-latest" => rid.StartsWith("win", StringComparison.OrdinalIgnoreCase),
            "macos-latest" => rid.StartsWith("osx", StringComparison.OrdinalIgnoreCase)
                || rid.StartsWith("maccatalyst", StringComparison.OrdinalIgnoreCase),
            _ => rid.StartsWith("linux", StringComparison.OrdinalIgnoreCase)
                || rid.StartsWith("unix", StringComparison.OrdinalIgnoreCase),
        };
    }

}

internal sealed record DotnetSetupPlan(
    string Mode,
    IReadOnlyList<DotnetRuntimeRequirement> RequiredRuntimes,
    string Source,
    string? Error);

internal sealed record ToolAssetTarget(
    string DirectoryPath,
    string Rid,
    DotnetRuntimeRequirement Requirement,
    bool HasPlatformSuffix);
