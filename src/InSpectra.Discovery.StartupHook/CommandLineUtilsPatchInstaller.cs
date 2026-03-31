using System.Reflection;
using HarmonyLib;

internal static class CommandLineUtilsPatchInstaller
{
    internal static Assembly? FrameworkAssembly;
    internal static string? CliFramework;
    internal static string? CapturePath;

    private static readonly List<string> PatchLog = [];
    private static volatile bool _captured;
    private static object? _capturedRootApplication;
    private static string? _noPatchableMethodDiagnostic;

    public static void Install(Assembly assembly, string cliFramework, string capturePath)
    {
        FrameworkAssembly = assembly;
        CliFramework = cliFramework;
        CapturePath = capturePath;
        PatchLog.Clear();
        _captured = false;
        _capturedRootApplication = null;
        _noPatchableMethodDiagnostic = null;

        var harmony = new Harmony("com.inspectra.discovery.startuphook.commandlineutils");
        var parsePostfix = new HarmonyMethod(typeof(CommandLineUtilsPatchInstaller), nameof(ParsePostfix));
        var executePostfix = new HarmonyMethod(typeof(CommandLineUtilsPatchInstaller), nameof(ExecutePostfix));
        var executeFinalizer = new HarmonyMethod(typeof(CommandLineUtilsPatchInstaller), nameof(ExecuteFinalizer));
        var constructorPostfix = new HarmonyMethod(typeof(CommandLineUtilsPatchInstaller), nameof(CommandLineApplicationConstructorPostfix));

        var patchCount = 0;
        patchCount += TryPatchNamedMethods(harmony, assembly, "Parse", parsePostfix);
        patchCount += TryPatchNamedMethods(harmony, assembly, "Execute", executePostfix, executeFinalizer);
        patchCount += TryPatchNamedMethods(harmony, assembly, "ExecuteAsync", executePostfix, executeFinalizer);
        patchCount += TryPatchConstructors(harmony, assembly, constructorPostfix);

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        if (patchCount == 0)
        {
            var diagnostic = new System.Text.StringBuilder();
            diagnostic.AppendLine($"Framework: {cliFramework}");
            diagnostic.AppendLine($"Assembly: {assembly.FullName}");
            diagnostic.AppendLine($"Patch log: {string.Join("; ", PatchLog)}");
            _noPatchableMethodDiagnostic = diagnostic.ToString();
        }
    }

    public static void CommandLineApplicationConstructorPostfix(object __instance)
    {
        if (_captured || __instance is null)
        {
            return;
        }

        _capturedRootApplication = NavigateToRoot(__instance);
    }

    public static void ParsePostfix(object? __instance)
    {
        if (_captured || __instance is null)
        {
            return;
        }

        TryCaptureFromObject(__instance, "Parse-postfix");
    }

    public static void ExecutePostfix(object? __instance)
    {
        if (_captured)
        {
            return;
        }

        if (__instance is not null && TryCaptureFromObject(__instance, "Execute-postfix"))
        {
            return;
        }

        if (_capturedRootApplication is not null)
        {
            TryCaptureFromObject(_capturedRootApplication, "Execute-postfix");
        }
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        if (_captured)
        {
            return;
        }

        var rootApplication = _capturedRootApplication ?? FindRootApplicationFromLoadedTypes();
        if (rootApplication is not null && TryCaptureFromObject(rootApplication, "ProcessExit-fallback"))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_noPatchableMethodDiagnostic) && CapturePath is not null)
        {
            CaptureFileWriter.WriteError(CapturePath, "no-patchable-method", _noPatchableMethodDiagnostic);
        }
    }

    public static Exception? ExecuteFinalizer(object? __instance, Exception? __exception)
    {
        if (_captured)
        {
            return __exception;
        }

        if (__instance is not null && TryCaptureFromObject(__instance, "Execute-finalizer"))
        {
            return __exception;
        }

        if (_capturedRootApplication is not null)
        {
            TryCaptureFromObject(_capturedRootApplication, "Execute-finalizer");
        }

        return __exception;
    }

    private static int TryPatchNamedMethods(Harmony harmony, Assembly assembly, string methodName, HarmonyMethod postfix, HarmonyMethod? finalizer = null)
    {
        var count = 0;
        foreach (var type in assembly.GetExportedTypes().Where(IsCommandLineApplicationType))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)
                    || method.IsSpecialName)
                {
                    continue;
                }

                try
                {
                    harmony.Patch(method, postfix: postfix, finalizer: finalizer);
                    PatchLog.Add($"OK: {type.Name}.{method.Name}");
                    count++;
                }
                catch (Exception ex)
                {
                    PatchLog.Add($"FAIL: {type.Name}.{method.Name}: {ex.Message}");
                }
            }
        }

        return count;
    }

    private static int TryPatchConstructors(Harmony harmony, Assembly assembly, HarmonyMethod postfix)
    {
        var count = 0;
        foreach (var type in assembly.GetExportedTypes().Where(IsCommandLineApplicationType))
        {
            foreach (var constructor in type.GetConstructors())
            {
                try
                {
                    harmony.Patch(constructor, postfix: postfix);
                    PatchLog.Add($"OK: {type.Name}.ctor({string.Join(", ", constructor.GetParameters().Select(parameter => parameter.ParameterType.Name))})");
                    count++;
                }
                catch (Exception ex)
                {
                    PatchLog.Add($"FAIL: {type.Name}.ctor: {ex.Message}");
                }
            }
        }

        return count;
    }

    private static bool TryCaptureFromObject(object target, string source)
    {
        if (_captured || FrameworkAssembly is null || CapturePath is null || string.IsNullOrWhiteSpace(CliFramework))
        {
            return false;
        }

        var rootApplication = ResolveRootApplication(target);
        if (rootApplication is null)
        {
            return false;
        }

        try
        {
            var version = FrameworkAssembly.GetName().Version?.ToString();
            CaptureFileWriter.Write(CapturePath, new CaptureResult
            {
                Status = "ok",
                CliFramework = CliFramework,
                FrameworkVersion = version,
                SystemCommandLineVersion = string.Equals(CliFramework, HookCliFrameworkSupport.SystemCommandLine, StringComparison.Ordinal)
                    ? version
                    : null,
                PatchTarget = $"{source} ({string.Join(", ", PatchLog.Where(entry => entry.StartsWith("OK", StringComparison.Ordinal)))})",
                Root = CommandLineUtilsTreeWalker.Walk(rootApplication),
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

    private static object? ResolveRootApplication(object instance)
    {
        return IsCommandLineApplicationType(instance.GetType())
            ? NavigateToRoot(instance)
            : _capturedRootApplication ?? FindRootApplicationFromLoadedTypes();
    }

    private static object NavigateToRoot(object application)
    {
        var current = application;
        for (var i = 0; i < 100; i++)
        {
            var parent = ReflectionValueReader.GetMemberValue(current, "Parent");
            if (parent is null || !IsCommandLineApplicationType(parent.GetType()))
            {
                break;
            }

            current = parent;
        }

        return current;
    }

    private static object? FindRootApplicationFromLoadedTypes()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        object? value;
                        try
                        {
                            value = field.GetValue(null);
                        }
                        catch
                        {
                            continue;
                        }

                        if (value is not null && IsCommandLineApplicationType(value.GetType()))
                        {
                            return NavigateToRoot(value);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool IsCommandLineApplicationType(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var fullName = current.FullName;
            if (fullName is null || string.IsNullOrWhiteSpace(CliFramework))
            {
                continue;
            }

            if (fullName.StartsWith(CliFramework + ".CommandLineApplication", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
