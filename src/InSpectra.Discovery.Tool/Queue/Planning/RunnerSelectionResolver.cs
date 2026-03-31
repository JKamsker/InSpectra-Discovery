namespace InSpectra.Discovery.Tool.Queue.Planning;

using InSpectra.Discovery.Tool.NuGet;

using InSpectra.Discovery.Tool.Queue.Models;

using System.IO.Compression;
using System.Text.Json.Nodes;

internal static class RunnerSelectionResolver
{
    public static RunnerSelection GetHistoricalHint(string repositoryRoot, string packageId)
    {
        var stateDirectory = Path.Combine(repositoryRoot, "state", "packages", packageId.ToLowerInvariant());
        if (!Directory.Exists(stateDirectory))
        {
            return new RunnerSelection("ubuntu-latest", "default-ubuntu-package-history", [], [], [], null, "default");
        }

        foreach (var stateFile in Directory.GetFiles(stateDirectory, "*.json").OrderByDescending(Path.GetFileName))
        {
            JsonObject? state;
            try
            {
                state = JsonNode.Parse(File.ReadAllText(stateFile))?.AsObject();
            }
            catch
            {
                continue;
            }

            var failureText = string.Join(
                Environment.NewLine,
                new[]
                {
                    state?["lastFailureSignature"]?.GetValue<string>(),
                    state?["lastFailureMessage"]?.GetValue<string>(),
                }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (failureText.Contains("Microsoft.WindowsDesktop.App", StringComparison.OrdinalIgnoreCase))
            {
                return new RunnerSelection(
                    "windows-latest",
                    "historical-state-microsoft.windowsdesktop.app",
                    ["Microsoft.WindowsDesktop.App"],
                    [],
                    [],
                    null,
                    "historical-state");
            }
        }

        return new RunnerSelection("ubuntu-latest", "default-ubuntu-package-history", [], [], [], null, "default");
    }

    public static async Task<RunnerSelection> ResolveForPlanItemAsync(
        string repositoryRoot,
        JsonObject item,
        CatalogLeaf? catalogLeaf,
        bool skipRunnerInspection,
        NuGetApiClient client,
        CancellationToken cancellationToken)
    {
        var precomputed = GetPrecomputed(item, skipRunnerInspection);
        if (precomputed is not null)
        {
            return precomputed;
        }

        var packageId = item["packageId"]?.GetValue<string>() ?? string.Empty;
        var historicalHint = string.IsNullOrWhiteSpace(packageId)
            ? null
            : GetHistoricalHint(repositoryRoot, packageId);

        var catalogSelection = TryResolveFromCatalog(catalogLeaf);
        if (catalogSelection is not null)
        {
            return PreferHistoricalHint(catalogSelection, historicalHint);
        }

        if (skipRunnerInspection)
        {
            return historicalHint ?? new RunnerSelection("ubuntu-latest", "queue-skip-runner-inspection", [], [], [], null, "queue");
        }

        return await InspectPackageAsync(client, item["packageContentUrl"]?.GetValue<string>(), cancellationToken);
    }

    internal static RunnerSelection? TryResolveFromCatalog(CatalogLeaf? catalogLeaf)
    {
        if (catalogLeaf?.PackageEntries is null || catalogLeaf.PackageEntries.Count == 0)
        {
            return null;
        }

        var requiredFrameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toolRids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var runtimeRids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in catalogLeaf.PackageEntries)
        {
            var entryPath = entry.FullName.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(entryPath))
            {
                continue;
            }

            var segments = entryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 4 && segments[0] == "tools")
            {
                AddRequiredFramework(requiredFrameworks, segments[1]);

                var rid = segments[2];
                if (!string.IsNullOrWhiteSpace(rid) && !string.Equals(rid, "any", StringComparison.OrdinalIgnoreCase))
                {
                    toolRids.Add(rid);
                }
            }

            if (segments.Length >= 3 && segments[0] == "runtimes")
            {
                runtimeRids.Add(segments[1]);
            }
        }

        return SelectRunner(
            requiredFrameworks.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            toolRids.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            runtimeRids.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            inspectionError: null,
            hintSource: "catalog");
    }

    private static RunnerSelection? GetPrecomputed(JsonObject item, bool skipRunnerInspection)
    {
        var runsOn = item["runsOn"]?.GetValue<string>();
        if (!skipRunnerInspection && string.IsNullOrWhiteSpace(runsOn))
        {
            return null;
        }

        return new RunnerSelection(
            string.IsNullOrWhiteSpace(runsOn) ? "ubuntu-latest" : runsOn,
            item["runnerReason"]?.GetValue<string>() ?? (skipRunnerInspection ? "queue-skip-runner-inspection" : "precomputed-runner-selection"),
            item["requiredFrameworks"]?.AsArray().Select(node => node?.GetValue<string>() ?? string.Empty).Where(value => value.Length > 0).ToList() ?? [],
            item["toolRids"]?.AsArray().Select(node => node?.GetValue<string>() ?? string.Empty).Where(value => value.Length > 0).ToList() ?? [],
            item["runtimeRids"]?.AsArray().Select(node => node?.GetValue<string>() ?? string.Empty).Where(value => value.Length > 0).ToList() ?? [],
            item["inspectionError"]?.GetValue<string>(),
            skipRunnerInspection ? "queue" : "precomputed");
    }

    private static async Task<RunnerSelection> InspectPackageAsync(
        NuGetApiClient client,
        string? packageContentUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageContentUrl))
        {
            return new RunnerSelection("ubuntu-latest", "default-ubuntu", [], [], [], null, "inspection");
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"inspectra-batch-{Guid.NewGuid():N}.nupkg");
        try
        {
            await client.DownloadFileAsync(packageContentUrl, tempFile, cancellationToken);
            using var archive = ZipFile.OpenRead(tempFile);
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toolRids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var runtimeRids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? inspectionError = null;

            foreach (var entry in archive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/').Trim('/');
                if (string.IsNullOrWhiteSpace(entryPath))
                {
                    continue;
                }

                var segments = entryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 4 && segments[0] == "tools")
                {
                    var rid = segments[2];
                    if (!string.IsNullOrWhiteSpace(rid) && !string.Equals(rid, "any", StringComparison.OrdinalIgnoreCase))
                    {
                        toolRids.Add(rid);
                    }
                }

                if (segments.Length >= 3 && segments[0] == "runtimes")
                {
                    runtimeRids.Add(segments[1]);
                }

                if (!entryPath.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    using var reader = new StreamReader(entry.Open());
                    var runtimeConfig = JsonNode.Parse(await reader.ReadToEndAsync(cancellationToken))?.AsObject();
                    var runtimeOptions = runtimeConfig?["runtimeOptions"]?.AsObject();
                    AddFrameworkName(frameworks, runtimeOptions?["framework"]);
                    foreach (var frameworkNode in runtimeOptions?["frameworks"]?.AsArray() ?? [])
                    {
                        AddFrameworkName(frameworks, frameworkNode);
                    }
                }
                catch (Exception ex)
                {
                    inspectionError = ex.Message;
                }
            }

            var requiredFrameworks = frameworks.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
            var toolRidList = toolRids.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
            var runtimeRidList = runtimeRids.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
            return SelectRunner(requiredFrameworks, toolRidList, runtimeRidList, inspectionError, "inspection");
        }
        catch (Exception ex)
        {
            return new RunnerSelection("ubuntu-latest", "default-ubuntu-inspection-failed", [], [], [], ex.Message, "inspection");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void AddFrameworkName(HashSet<string> frameworks, JsonNode? node)
    {
        if (node is null)
        {
            return;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var stringValue) && !string.IsNullOrWhiteSpace(stringValue))
        {
            frameworks.Add(stringValue.Trim());
            return;
        }

        if (node["name"]?.GetValue<string>() is { Length: > 0 } namedFramework)
        {
            frameworks.Add(namedFramework.Trim());
        }
    }

    private static void AddRequiredFramework(HashSet<string> frameworks, string targetFrameworkMoniker)
    {
        if (string.IsNullOrWhiteSpace(targetFrameworkMoniker))
        {
            return;
        }

        if (targetFrameworkMoniker.Contains("windows", StringComparison.OrdinalIgnoreCase))
        {
            frameworks.Add("Microsoft.WindowsDesktop.App");
            return;
        }

        if (targetFrameworkMoniker.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase)
            || (targetFrameworkMoniker.StartsWith("net", StringComparison.OrdinalIgnoreCase)
                && targetFrameworkMoniker.Length > 3
                && char.IsDigit(targetFrameworkMoniker[3])))
        {
            frameworks.Add("Microsoft.NETCore.App");
        }
    }

    private static RunnerSelection PreferHistoricalHint(RunnerSelection selection, RunnerSelection? historicalHint)
    {
        if (historicalHint is null || historicalHint.RunsOn == "ubuntu-latest" || selection.RunsOn != "ubuntu-latest")
        {
            return selection;
        }

        return historicalHint with { HintSource = "historical-state" };
    }

    internal static RunnerSelection SelectRunner(
        IReadOnlyList<string> requiredFrameworks,
        IReadOnlyList<string> toolRids,
        IReadOnlyList<string> runtimeRids,
        string? inspectionError,
        string hintSource)
    {
        if (requiredFrameworks.Contains("Microsoft.WindowsDesktop.App", StringComparer.OrdinalIgnoreCase))
        {
            return new RunnerSelection("windows-latest", "framework-microsoft.windowsdesktop.app", requiredFrameworks, toolRids, runtimeRids, inspectionError, hintSource);
        }

        if (toolRids.Count > 0 && toolRids.All(IsWindowsRid))
        {
            return new RunnerSelection("windows-latest", "tool-rids-windows-only", requiredFrameworks, toolRids, runtimeRids, inspectionError, hintSource);
        }

        if (runtimeRids.Count > 0 && runtimeRids.All(IsWindowsRid))
        {
            return new RunnerSelection("windows-latest", "runtime-rids-windows-only", requiredFrameworks, toolRids, runtimeRids, inspectionError, hintSource);
        }

        if (toolRids.Count > 0 && toolRids.All(IsMacOsRid))
        {
            return new RunnerSelection("macos-latest", "tool-rids-macos-only", requiredFrameworks, toolRids, runtimeRids, inspectionError, hintSource);
        }

        if (runtimeRids.Count > 0 && runtimeRids.All(IsMacOsRid))
        {
            return new RunnerSelection("macos-latest", "runtime-rids-macos-only", requiredFrameworks, toolRids, runtimeRids, inspectionError, hintSource);
        }

        return new RunnerSelection("ubuntu-latest", "default-ubuntu", requiredFrameworks, toolRids, runtimeRids, inspectionError, hintSource);
    }

    private static bool IsWindowsRid(string rid)
        => rid.StartsWith("win", StringComparison.OrdinalIgnoreCase);

    private static bool IsMacOsRid(string rid)
        => rid.StartsWith("osx", StringComparison.OrdinalIgnoreCase) || rid.StartsWith("maccatalyst", StringComparison.OrdinalIgnoreCase);
}
