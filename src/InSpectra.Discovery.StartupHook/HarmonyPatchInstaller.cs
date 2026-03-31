using System.Reflection;
using HarmonyLib;

internal static class HarmonyPatchInstaller
{
    internal static Assembly? SystemCommandLineAssembly;
    internal static string? CapturePath;
    private static volatile bool _captured;
    private static readonly List<string> _patchLog = [];
    private static object? _capturedRootCommand;

    public static string GetPatchLog() => string.Join("\n", _patchLog);

    public static void Install(Assembly sclAssembly, string capturePath)
    {
        SystemCommandLineAssembly = sclAssembly;
        CapturePath = capturePath;

        var harmony = new Harmony("com.inspectra.discovery.startuphook");

        var postfix = new HarmonyMethod(typeof(HarmonyPatchInstaller), nameof(ParsePostfix));
        var invokePostfix = new HarmonyMethod(typeof(HarmonyPatchInstaller), nameof(InvokePostfix));
        var patchCount = 0;

        // Parse methods capture the ParseResult return value when the public API exposes it.
        patchCount += TryPatchAll(harmony, sclAssembly, "Parse", postfix: postfix);

        // Invoke methods are observed after they return so the hook never changes target control flow.
        patchCount += TryPatchAll(harmony, sclAssembly, "Invoke", postfix: invokePostfix);
        patchCount += TryPatchAll(harmony, sclAssembly, "InvokeAsync", postfix: invokePostfix);

        // Fallback: patch RootCommand constructors to capture the instance.
        // When Harmony patches on public API methods don't fire (e.g., R2R precompiled tools
        // or tools that use internal API paths), we capture the RootCommand via its constructor
        // and serialize it on ProcessExit.
        var rootCommandType = sclAssembly.GetType("System.CommandLine.RootCommand");
        if (rootCommandType is not null)
        {
            var ctorPostfix = new HarmonyMethod(typeof(HarmonyPatchInstaller), nameof(RootCommandCtorPostfix));
            foreach (var ctor in rootCommandType.GetConstructors())
            {
                try
                {
                    harmony.Patch(ctor, postfix: ctorPostfix);
                    _patchLog.Add($"OK: RootCommand.ctor({string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name))})");
                    patchCount++;
                }
                catch (Exception ex)
                {
                    _patchLog.Add($"FAIL: RootCommand.ctor: {ex.Message}");
                }
            }
        }

        // Register ProcessExit to capture from a stored RootCommand if earlier patches never resolved one.
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        if (patchCount == 0)
        {
            var diag = new System.Text.StringBuilder();
            diag.AppendLine($"Assembly: {sclAssembly.FullName}");
            diag.AppendLine($"Patch log: {string.Join("; ", _patchLog)}");
            CaptureFileWriter.WriteError(capturePath, "no-patchable-method", diag.ToString());
        }
    }

    private static int TryPatchAll(Harmony harmony, Assembly assembly, string methodName,
        HarmonyMethod? prefix = null, HarmonyMethod? postfix = null)
    {
        var count = 0;

        // Search ALL exported types for methods with this name.
        foreach (var type in assembly.GetExportedTypes())
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == methodName)
                // Skip property getters like get_ParseResult, delegate Invoke, etc.
                .Where(m => !m.IsSpecialName)
                // Only patch methods that deal with command-line types (not delegates, etc.)
                .Where(m => IsCommandLineRelated(m))
                .ToArray();

            foreach (var method in methods)
            {
                try
                {
                    harmony.Patch(method, prefix: prefix, postfix: postfix);
                    _patchLog.Add($"OK: {type.Name}.{method.Name}");
                    count++;
                }
                catch (Exception ex)
                {
                    _patchLog.Add($"FAIL: {type.Name}.{method.Name}: {ex.Message}");
                }
            }
        }

        return count;
    }

    private static bool IsCommandLineRelated(MethodInfo method)
    {
        // Filter out delegate Invoke methods, property accessors, etc.
        var declaringType = method.DeclaringType;
        if (declaringType is null) return false;

        // Skip if the declaring type is a delegate
        if (typeof(Delegate).IsAssignableFrom(declaringType)) return false;

        // For Parse: must return something (ParseResult)
        if (method.Name == "Parse" && method.ReturnType == typeof(void)) return false;

        // For Invoke: must have at least one parameter that looks like a command-line type
        // (or be on a type that IS a command-line type)
        var typeFullName = declaringType.FullName ?? "";
        return typeFullName.StartsWith("System.CommandLine", StringComparison.Ordinal);
    }

    /// <summary>
    /// Postfix on RootCommand constructor — captures the instance when created.
    /// </summary>
    public static void RootCommandCtorPostfix(object __instance)
    {
        _capturedRootCommand = __instance;
    }

    /// <summary>
    /// ProcessExit handler — if no earlier patch fired, serialize from captured RootCommand.
    /// </summary>
    private static void OnProcessExit(object? sender, EventArgs e)
    {
        if (_captured || CapturePath is null || SystemCommandLineAssembly is null) return;

        var root = _capturedRootCommand ?? FindRootCommandFromLoadedTypes();
        if (root is not null)
        {
            TryCaptureFromObject(root, "ProcessExit-fallback");
        }
    }

    /// <summary>
    /// Postfix on Parse methods — fires AFTER Parse returns.
    /// The __result is the ParseResult, from which we extract the Command tree.
    /// </summary>
    public static void ParsePostfix(object? __instance, object? __result)
    {
        if (_captured || __result is null) return;
        TryCaptureFromObject(__result, "Parse-postfix");
    }

    /// <summary>
    /// Postfix on Invoke/InvokeAsync — observes the command surface without short-circuiting the target.
    /// </summary>
    public static void InvokePostfix(object? __instance)
    {
        if (_captured || __instance is null) return;
        TryCaptureFromObject(__instance, "Invoke-postfix");
    }

    private static bool TryCaptureFromObject(object? target, string source)
    {
        if (_captured || target is null || SystemCommandLineAssembly is null || CapturePath is null)
            return false;

        var rootCommand = ResolveRootCommand(target);
        if (rootCommand is null)
            return false;

        try
        {
            var tree = CommandTreeWalker.Walk(rootCommand, SystemCommandLineAssembly);
            var version = SystemCommandLineAssembly.GetName().Version?.ToString();
            CaptureFileWriter.Write(CapturePath, new CaptureResult
            {
                Status = "ok",
                CliFramework = HookCliFrameworkSupport.SystemCommandLine,
                FrameworkVersion = version,
                SystemCommandLineVersion = version,
                PatchTarget = $"{source} ({string.Join(", ", _patchLog.Where(l => l.StartsWith("OK")))})",
                Root = tree,
            });
            _captured = true;
            return true;
        }
        catch (Exception ex)
        {
            CaptureFileWriter.WriteError(CapturePath, "capture-failed", ex.ToString());
            return false;
        }
    }

    private static object? ResolveRootCommand(object instance)
    {
        // Direct Command check
        if (IsCommandType(instance.GetType()))
            return NavigateToRoot(instance);

        // ParseResult → RootCommandResult.Command or CommandResult.Command
        var rootCmd = TryNavigateProperty(instance, "RootCommandResult", "Command")
                   ?? TryNavigateProperty(instance, "CommandResult", "Command");
        if (rootCmd is not null) return NavigateToRoot(rootCmd);

        // Direct RootCommand property
        rootCmd = GetPropertyValue(instance, "RootCommand");
        if (rootCmd is not null && IsCommandType(rootCmd.GetType()))
            return rootCmd;

        // Parser → Configuration.RootCommand
        var config = GetPropertyValue(instance, "Configuration");
        if (config is not null)
        {
            rootCmd = GetPropertyValue(config, "RootCommand");
            if (rootCmd is not null && IsCommandType(rootCmd.GetType()))
                return rootCmd;
        }

        // Last resort: scan static fields
        return FindRootCommandFromLoadedTypes();
    }

    private static object? TryNavigateProperty(object instance, string first, string second)
    {
        try
        {
            var intermediate = GetPropertyValue(instance, first);
            if (intermediate is null) return null;
            var result = GetPropertyValue(intermediate, second);
            return result is not null && IsCommandType(result.GetType()) ? result : null;
        }
        catch { return null; }
    }

    private static object? GetPropertyValue(object obj, string name)
    {
        try
        {
            return obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj);
        }
        catch { return null; }
    }

    private static object NavigateToRoot(object command)
    {
        var current = command;
        for (var i = 0; i < 100; i++)
        {
            var parent = GetPropertyValue(current, "Parent");
            if (parent is null || !IsCommandType(parent.GetType()))
                break;
            current = parent;
        }
        return current;
    }

    private static object? FindRootCommandFromLoadedTypes()
    {
        if (SystemCommandLineAssembly is null) return null;
        var rootCommandType = SystemCommandLineAssembly.GetType("System.CommandLine.RootCommand");
        var commandType = SystemCommandLineAssembly.GetType("System.CommandLine.Command");
        if (rootCommandType is null && commandType is null) return null;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;
            try
            {
                foreach (var type in assembly.GetTypes())
                    foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        try
                        {
                            if ((rootCommandType?.IsAssignableFrom(field.FieldType) ?? false)
                                || (commandType?.IsAssignableFrom(field.FieldType) ?? false))
                            {
                                var value = field.GetValue(null);
                                if (value is not null) return value;
                            }
                        }
                        catch { }
                    }
            }
            catch { }
        }
        return null;
    }

    private static bool IsCommandType(Type type)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t.FullName is "System.CommandLine.Command" or "System.CommandLine.RootCommand")
                return true;
        }
        return false;
    }
}
