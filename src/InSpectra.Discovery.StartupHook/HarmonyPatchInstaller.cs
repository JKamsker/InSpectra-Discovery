using System.Reflection;
using HarmonyLib;

internal static class HarmonyPatchInstaller
{
    internal static Assembly? SystemCommandLineAssembly;
    internal static string? CapturePath;

    public static void Install(Assembly sclAssembly, string capturePath)
    {
        SystemCommandLineAssembly = sclAssembly;
        CapturePath = capturePath;

        var harmony = new Harmony("com.inspectra.discovery.startuphook");
        var patchMethod = new HarmonyMethod(typeof(HarmonyPatchInstaller), nameof(InvocationPrefix));

        var patched = TryPatch(harmony, sclAssembly, patchMethod);

        if (!patched)
        {
            // Dump diagnostic info about available types and methods.
            var diag = new System.Text.StringBuilder();
            diag.AppendLine($"Assembly: {sclAssembly.FullName}");
            foreach (var type in sclAssembly.GetExportedTypes().OrderBy(t => t.FullName))
            {
                var invokeMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => m.Name.Contains("Invoke") || m.Name.Contains("Parse"))
                    .Select(m => $"  {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                    .ToList();
                if (invokeMethods.Count > 0)
                {
                    diag.AppendLine($"\n{type.FullName}:");
                    foreach (var m in invokeMethods)
                        diag.AppendLine(m);
                }
            }
            if (_lastPatchError is not null)
                diag.AppendLine($"\nLast patch error: {_lastPatchError}");
            CaptureFileWriter.WriteError(capturePath, "no-patchable-method", diag.ToString());
        }
    }

    private static bool TryPatch(Harmony harmony, Assembly assembly, HarmonyMethod prefix)
    {
        // Strategy: try to patch invocation entry points across all known S.CL API versions.
        // Each candidate is (TypeName, MethodName, SearchStatic).

        (string TypeName, string MethodName, bool Static)[] candidates =
        [
            // v2.0.5+ (stable): ParseResult.InvokeAsync / .Invoke (instance)
            ("System.CommandLine.ParseResult", "InvokeAsync", false),
            ("System.CommandLine.ParseResult", "Invoke", false),

            // v2.0.0-beta: ParseResultExtensions (static)
            ("System.CommandLine.Parsing.ParseResultExtensions", "InvokeAsync", true),
            ("System.CommandLine.Parsing.ParseResultExtensions", "Invoke", true),

            // v2.0.0-beta: ParserExtensions (static)
            ("System.CommandLine.Parsing.ParserExtensions", "InvokeAsync", true),
            ("System.CommandLine.Parsing.ParserExtensions", "Invoke", true),

            // v2.0.0-beta: CommandExtensions (static)
            ("System.CommandLine.CommandExtensions", "InvokeAsync", true),
            ("System.CommandLine.CommandExtensions", "Invoke", true),
            ("System.CommandLine.Invocation.CommandExtensions", "InvokeAsync", true),
            ("System.CommandLine.Invocation.CommandExtensions", "Invoke", true),

            // v2.0.0-beta: CommandLineConfiguration (instance)
            ("System.CommandLine.CommandLineConfiguration", "InvokeAsync", false),
            ("System.CommandLine.CommandLineConfiguration", "Invoke", false),

            // v2.0.0-beta: Parser instance methods
            ("System.CommandLine.Parsing.Parser", "InvokeAsync", false),
            ("System.CommandLine.Parsing.Parser", "Invoke", false),

            // v2.0.5+: HelpAction.Invoke (instance)
            ("System.CommandLine.Help.HelpAction", "Invoke", false),
        ];

        foreach (var (typeName, methodName, isStatic) in candidates)
        {
            var type = assembly.GetType(typeName);
            if (type is null) continue;

            var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            // Try each matching overload individually to avoid ambiguity.
            var methods = type.GetMethods(flags).Where(m => m.Name == methodName).ToArray();

            foreach (var method in methods)
            {
                try
                {
                    harmony.Patch(method, prefix);
                    _patchTargetName = $"{typeName}.{methodName}";
                    return true;
                }
                catch (Exception ex)
                {
                    _lastPatchError = $"{typeName}.{methodName}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))}): {ex.Message}";
                }
            }
        }

        return false;
    }

    private static string? _patchTargetName;
    private static string? _lastPatchError;

    /// <summary>
    /// Harmony prefix — fires before InvokeAsync/Invoke.
    /// Returns false to skip the original method.
    /// </summary>
    /// <summary>
    /// Prefix for static extension methods — first arg (__0) is the Command/Parser object.
    /// </summary>
    public static bool StaticInvocationPrefix(object __0)
    {
        return InvocationPrefix(__0, null);
    }

    public static bool InvocationPrefix(object? __instance, MethodBase? __originalMethod)
    {
        try
        {
            object? target = __instance;

            // For static methods, __instance is null. Try to find a Command-like parameter
            // in the method's arguments by scanning loaded types.
            if (target is null)
            {
                target = FindRootCommandFromLoadedTypes();
            }

            if (target is null)
            {
                CaptureFileWriter.WriteError(CapturePath!, "null-instance",
                    $"Patched method received null instance. Method: {__originalMethod?.DeclaringType?.FullName}.{__originalMethod?.Name}");
                Environment.Exit(0);
                return false;
            }

            // Find the root Command from whatever object we intercepted.
            var rootCommand = ResolveRootCommand(target);
            if (rootCommand is not null)
            {
                var tree = CommandTreeWalker.Walk(rootCommand, SystemCommandLineAssembly!);
                var version = SystemCommandLineAssembly!.GetName().Version?.ToString();
                CaptureFileWriter.Write(CapturePath!, new CaptureResult
                {
                    Status = "ok",
                    SystemCommandLineVersion = version,
                    PatchTarget = _patchTargetName ?? target.GetType().FullName,
                    Root = tree,
                });
            }
            else
            {
                CaptureFileWriter.WriteError(CapturePath!, "no-root-command",
                    $"Could not resolve root command from {target.GetType().FullName}");
            }
        }
        catch (Exception ex)
        {
            CaptureFileWriter.WriteError(CapturePath!, "capture-failed", ex.ToString());
        }

        Environment.Exit(0);
        return false;
    }

    /// <summary>
    /// Harmony prefix for static extension methods where the first arg is the Command/ParseResult.
    /// </summary>
    public static bool StaticInvocationPrefix(object? __0, MethodBase __originalMethod)
        => InvocationPrefix(__0, __originalMethod);

    /// <summary>
    /// Last resort: scan loaded assemblies for static fields of type RootCommand or Command.
    /// This handles tools where the root command is stored in a static field.
    /// </summary>
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
                {
                    foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        try
                        {
                            if ((rootCommandType is not null && rootCommandType.IsAssignableFrom(field.FieldType))
                                || (commandType is not null && commandType.IsAssignableFrom(field.FieldType)))
                            {
                                var value = field.GetValue(null);
                                if (value is not null)
                                    return value;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        return null;
    }

    private static object? ResolveRootCommand(object instance)
    {
        var type = instance.GetType();

        // If the instance IS a Command (or RootCommand), use it directly.
        if (IsCommandType(type))
            return NavigateToRoot(instance);

        // Try multiple navigation strategies, each guarded against failures.
        // ParseResult → .RootCommandResult.Command or .CommandResult.Command
        try
        {
            var rootCmdResult = GetPropertyValue(instance, "RootCommandResult");
            if (rootCmdResult is not null)
            {
                var cmd = GetPropertyValue(rootCmdResult, "Command");
                if (cmd is not null && IsCommandType(cmd.GetType()))
                    return NavigateToRoot(cmd);
            }
        }
        catch { /* Property may not exist in this S.CL version */ }

        try
        {
            var cmdResult = GetPropertyValue(instance, "CommandResult");
            if (cmdResult is not null)
            {
                var cmd = GetPropertyValue(cmdResult, "Command");
                if (cmd is not null && IsCommandType(cmd.GetType()))
                    return NavigateToRoot(cmd);
            }
        }
        catch { /* Property may not exist in this S.CL version */ }

        // CommandLineConfiguration / RootCommand property
        try
        {
            var rootCmd = GetPropertyValue(instance, "RootCommand");
            if (rootCmd is not null && IsCommandType(rootCmd.GetType()))
                return rootCmd;
        }
        catch { }

        // Parser → .Configuration.RootCommand
        var config = GetPropertyValue(instance, "Configuration");
        if (config is not null)
        {
            var rootCmd = GetPropertyValue(config, "RootCommand");
            if (rootCmd is not null && IsCommandType(rootCmd.GetType()))
                return rootCmd;
        }

        return null;
    }

    private static object? GetPropertyValue(object obj, string name)
    {
        try
        {
            return obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj);
        }
        catch
        {
            return null;
        }
    }

    private static object NavigateToRoot(object command)
    {
        // Walk up via Parent property to find the top-level command.
        var current = command;
        while (true)
        {
            var parentProp = current.GetType().GetProperty("Parent",
                BindingFlags.Public | BindingFlags.Instance);
            if (parentProp is null)
                break;

            var parent = parentProp.GetValue(current);
            if (parent is null || !IsCommandType(parent.GetType()))
                break;

            current = parent;
        }
        return current;
    }

    private static bool IsCommandType(Type type)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            var fullName = t.FullName;
            if (fullName is "System.CommandLine.Command" or "System.CommandLine.RootCommand")
                return true;
        }
        return false;
    }
}
