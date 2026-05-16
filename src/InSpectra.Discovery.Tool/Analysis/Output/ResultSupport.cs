namespace InSpectra.Discovery.Tool.Analysis.Output;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.Catalog.Filtering.SpectreConsole;

using InSpectra.Discovery.Tool.NuGet;


using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class ResultSupport
{
    public static JsonObject CreateInitialResult(string packageId, string version, string batchId, int attempt, string source, DateTimeOffset analyzedAt)
        => new()
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["batchId"] = batchId,
            ["attempt"] = attempt,
            ["trusted"] = false,
            ["source"] = source,
            ["analyzedAt"] = analyzedAt.ToString("O"),
            ["disposition"] = "retryable-failure",
            ["retryEligible"] = true,
            ["phase"] = "bootstrap",
            ["classification"] = "uninitialized",
            ["failureMessage"] = null,
            ["failureSignature"] = null,
            ["packageUrl"] = $"https://www.nuget.org/packages/{packageId}/{version}",
            ["totalDownloads"] = null,
            ["packageContentUrl"] = null,
            ["registrationLeafUrl"] = null,
            ["catalogEntryUrl"] = null,
            ["projectUrl"] = null,
            ["sourceRepositoryUrl"] = null,
            ["publishedAt"] = null,
            ["command"] = null,
            ["entryPoint"] = null,
            ["runner"] = null,
            ["toolSettingsPath"] = null,
            ["opencliSource"] = null,
            ["detection"] = new JsonObject
            {
                ["hasSpectreConsole"] = false,
                ["hasSpectreConsoleCli"] = false,
                ["matchedPackageEntries"] = new JsonArray(),
                ["matchedDependencyIds"] = new JsonArray(),
            },
            ["introspection"] = new JsonObject
            {
                ["opencli"] = null,
                ["xmldoc"] = null,
            },
            ["timings"] = new JsonObject
            {
                ["totalMs"] = null,
                ["installMs"] = null,
                ["opencliMs"] = null,
                ["xmldocMs"] = null,
            },
            ["steps"] = new JsonObject
            {
                ["install"] = null,
                ["opencli"] = null,
                ["xmldoc"] = null,
            },
            ["artifacts"] = new JsonObject
            {
                ["opencliArtifact"] = null,
                ["xmldocArtifact"] = null,
            },
        };

    public static DetectionInfo BuildDetection(CatalogLeaf catalogLeaf)
    {
        var matchedPackageEntries = catalogLeaf.PackageEntries?
            .Where(entry => entry.Name is "Spectre.Console.dll" or "Spectre.Console.Cli.dll")
            .Select(entry => entry.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        var matchedDependencyIds = catalogLeaf.DependencyGroups?
            .SelectMany(group => group.Dependencies ?? [])
            .Select(dependency => dependency.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id) && id.StartsWith("Spectre.Console", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        return new DetectionInfo(
            matchedPackageEntries.Any(entry => entry.EndsWith("Spectre.Console.dll", StringComparison.OrdinalIgnoreCase)),
            matchedPackageEntries.Any(entry => entry.EndsWith("Spectre.Console.Cli.dll", StringComparison.OrdinalIgnoreCase)) ||
            matchedDependencyIds.Contains("Spectre.Console.Cli", StringComparer.OrdinalIgnoreCase),
            matchedPackageEntries,
            matchedDependencyIds);
    }

    public static void MergePackageInspection(JsonObject detection, SpectrePackageInspection inspection)
    {
        detection["depsFilePaths"] = ToJsonArray(inspection.DepsFilePaths);
        detection["spectreConsoleDependencyVersions"] = ToJsonArray(inspection.SpectreConsoleDependencyVersions);
        detection["spectreConsoleCliDependencyVersions"] = ToJsonArray(inspection.SpectreConsoleCliDependencyVersions);
        detection["spectreConsoleAssemblies"] = JsonSerializer.SerializeToNode(inspection.SpectreConsoleAssemblies, JsonOptions.Default);
        detection["spectreConsoleCliAssemblies"] = JsonSerializer.SerializeToNode(inspection.SpectreConsoleCliAssemblies, JsonOptions.Default);
        detection["toolSettingsPaths"] = ToJsonArray(inspection.ToolSettingsPaths);
        detection["toolCommandNames"] = ToJsonArray(inspection.ToolCommandNames);
        detection["toolEntryPointPaths"] = ToJsonArray(inspection.ToolEntryPointPaths);
        detection["toolAssembliesReferencingSpectreConsole"] = ToJsonArray(inspection.ToolAssembliesReferencingSpectreConsole);
        detection["toolAssembliesReferencingSpectreConsoleCli"] = ToJsonArray(inspection.ToolAssembliesReferencingSpectreConsoleCli);
        detection["toolCliFrameworkReferences"] = JsonSerializer.SerializeToNode(inspection.ToolCliFrameworkReferences, JsonOptions.Default);
    }

    public static string GetFailureSignature(string phase, string classification, string? message)
    {
        var normalized = string.IsNullOrWhiteSpace(message)
            ? string.Empty
            : Regex.Replace(message, @"\s+", " ").Trim();
        return $"{phase}|{classification}|{normalized}";
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }
}

internal sealed record DetectionInfo(
    bool HasSpectreConsole,
    bool HasSpectreConsoleCli,
    IReadOnlyList<string> MatchedPackageEntries,
    IReadOnlyList<string> MatchedDependencyIds)
{
    public JsonObject ToJsonObject()
        => new()
        {
            ["hasSpectreConsole"] = HasSpectreConsole,
            ["hasSpectreConsoleCli"] = HasSpectreConsoleCli,
            ["matchedPackageEntries"] = ToJsonArray(MatchedPackageEntries),
            ["matchedDependencyIds"] = ToJsonArray(MatchedDependencyIds),
        };

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }
}
