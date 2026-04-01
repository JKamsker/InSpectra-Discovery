using System.Reflection;
using HarmonyLib;

internal static class CommandLineParserPatchInstaller
{
    internal static Assembly? FrameworkAssembly;
    internal static string? CapturePath;

    private static readonly List<string> PatchLog = [];
    private static volatile bool _captured;

    public static void Install(Assembly assembly, string capturePath)
    {
        FrameworkAssembly = assembly;
        CapturePath = capturePath;
        PatchLog.Clear();
        _captured = false;

        var harmony = new Harmony("com.inspectra.discovery.startuphook.commandlineparser");
        var parsePostfix = new HarmonyMethod(typeof(CommandLineParserPatchInstaller), nameof(ParsePostfix));

        var patchCount = 0;
        patchCount += TryPatchNamedMethods(harmony, assembly.GetType("CommandLine.Parser"), parsePostfix);
        patchCount += TryPatchNamedMethods(harmony, assembly.GetType("CommandLine.ParserExtensions"), parsePostfix);

        if (patchCount == 0 && CapturePath is not null)
        {
            var diagnostic = new System.Text.StringBuilder();
            diagnostic.AppendLine($"Assembly: {assembly.FullName}");
            diagnostic.AppendLine($"Patch log: {string.Join("; ", PatchLog)}");
            CaptureFileWriter.WriteError(CapturePath, "no-patchable-method", diagnostic.ToString());
        }
    }

    public static void ParsePostfix(MethodBase? __originalMethod, object? __result)
    {
        if (_captured || __result is null || FrameworkAssembly is null || CapturePath is null)
        {
            return;
        }

        if (!CommandLineParserTreeWalker.TryWalk(__result, out var root))
        {
            return;
        }

        try
        {
            CaptureFileWriter.Write(CapturePath, new CaptureResult
            {
                Status = "ok",
                CliFramework = HookCliFrameworkSupport.CommandLineParser,
                FrameworkVersion = FrameworkAssembly.GetName().Version?.ToString(),
                PatchTarget = FormatPatchTarget(__originalMethod),
                Root = root,
            });
            _captured = true;
        }
        catch (Exception ex)
        {
            CaptureFileWriter.WriteError(CapturePath, "capture-failed", ex.ToString());
        }
    }

    private static int TryPatchNamedMethods(Harmony harmony, Type? type, HarmonyMethod postfix)
    {
        if (type is null)
        {
            return 0;
        }

        var count = 0;
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (!string.Equals(method.Name, "ParseArguments", StringComparison.Ordinal) || method.IsSpecialName)
            {
                continue;
            }

            try
            {
                harmony.Patch(method, postfix: postfix);
                PatchLog.Add($"OK: {type.Name}.{method.Name}`{method.GetGenericArguments().Length}");
                count++;
            }
            catch (Exception ex)
            {
                PatchLog.Add($"FAIL: {type.Name}.{method.Name}: {ex.Message}");
            }
        }

        return count;
    }

    private static string FormatPatchTarget(MethodBase? method)
    {
        if (method is null)
        {
            return string.Join(", ", PatchLog.Where(entry => entry.StartsWith("OK", StringComparison.Ordinal)));
        }

        var methodName = $"{method.DeclaringType?.Name}.{method.Name}";
        var patchSummary = string.Join(", ", PatchLog.Where(entry => entry.StartsWith("OK", StringComparison.Ordinal)));
        return string.IsNullOrWhiteSpace(patchSummary)
            ? methodName
            : $"{methodName} ({patchSummary})";
    }
}
