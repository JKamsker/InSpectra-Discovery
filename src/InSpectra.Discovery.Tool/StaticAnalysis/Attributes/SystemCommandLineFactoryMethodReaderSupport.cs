namespace InSpectra.Discovery.Tool.StaticAnalysis.Attributes;

using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;
using InSpectra.Discovery.Tool.StaticAnalysis.Models;

using dnlib.DotNet;
using dnlib.DotNet.Emit;

internal static class SystemCommandLineFactoryMethodReaderSupport
{
    public static IReadOnlyDictionary<string, StaticCommandDefinition> Read(ModuleDefMD module)
    {
        var commands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var typeDef in module.GetTypes())
        {
            foreach (var method in typeDef.Methods.Where(CanBuildCommandSurface))
            {
                ReadMethodSurface(method, commands);
            }
        }

        return commands;
    }

    private static bool CanBuildCommandSurface(MethodDef method)
        => method.HasBody
            && !method.IsConstructor
            && !method.IsSpecialName
            && IsCommandType(method.MethodSig?.RetType?.ToTypeDefOrRef());

    private static void ReadMethodSurface(
        MethodDef method,
        IDictionary<string, StaticCommandDefinition> commands)
    {
        var stack = new List<MethodValue>();
        var locals = new Dictionary<int, MethodValue>();

        foreach (var instruction in method.Body.Instructions)
        {
            if (SystemCommandLineConstructorReaderSupport.TryReadInt32(instruction, out var intValue))
            {
                stack.Add(new Int32Value(intValue));
                continue;
            }

            switch (instruction.OpCode.Code)
            {
                case Code.Nop:
                    break;
                case Code.Ldstr:
                    stack.Add(new StringValue((string)instruction.Operand));
                    break;
                case Code.Ldnull:
                    stack.Add(NullValue.Instance);
                    break;
                case Code.Newarr:
                    stack.Add(BuildArrayValue(stack, instruction.Operand as ITypeDefOrRef));
                    break;
                case Code.Dup:
                    if (stack.Count > 0)
                    {
                        stack.Add(stack[^1]);
                    }

                    break;
                case Code.Pop:
                    Pop(stack);
                    break;
                case Code.Stelem_Ref:
                    ApplyArrayElementAssignment(stack);
                    break;
                case Code.Stloc:
                case Code.Stloc_S:
                case Code.Stloc_0:
                case Code.Stloc_1:
                case Code.Stloc_2:
                case Code.Stloc_3:
                    if (SystemCommandLineConstructorReaderSupport.TryGetLocalIndex(instruction, out var storeIndex))
                    {
                        locals[storeIndex] = Pop(stack);
                    }

                    break;
                case Code.Ldloc:
                case Code.Ldloc_S:
                case Code.Ldloc_0:
                case Code.Ldloc_1:
                case Code.Ldloc_2:
                case Code.Ldloc_3:
                    if (SystemCommandLineConstructorReaderSupport.TryGetLocalIndex(instruction, out var loadIndex))
                    {
                        stack.Add(locals.TryGetValue(loadIndex, out var value) ? value : UnknownValue.Instance);
                    }

                    break;
                case Code.Newobj:
                    stack.Add(BuildConstructedValue(stack, instruction.Operand as IMethod));
                    break;
                case Code.Call:
                case Code.Callvirt:
                    ApplyMethodCall(stack, instruction.Operand as IMethod, commands);
                    break;
                case Code.Ret:
                    if (stack.Count > 0 && stack[^1] is CommandValue returnedCommand)
                    {
                        Register(commands, returnedCommand);
                    }

                    break;
                default:
                    stack.Clear();
                    break;
            }
        }
    }

    private static MethodValue BuildArrayValue(List<MethodValue> stack, ITypeDefOrRef? elementType)
    {
        if (!string.Equals(elementType?.FullName, "System.String", StringComparison.Ordinal))
        {
            Pop(stack);
            return UnknownValue.Instance;
        }

        return Pop(stack) is Int32Value length && length.Value >= 0
            ? new StringArrayValue(length.Value)
            : UnknownValue.Instance;
    }

    private static void ApplyArrayElementAssignment(List<MethodValue> stack)
    {
        var value = Pop(stack);
        var index = Pop(stack) as Int32Value;
        var array = Pop(stack) as StringArrayValue;
        if (array is null || index is null || value is not StringValue stringValue)
        {
            return;
        }

        if (index.Value >= 0 && index.Value < array.Values.Length)
        {
            array.Values[index.Value] = stringValue.Value;
        }
    }

    private static MethodValue BuildConstructedValue(List<MethodValue> stack, IMethod? method)
    {
        if (method?.MethodSig is null)
        {
            return UnknownValue.Instance;
        }

        var arguments = PopArguments(stack, method.MethodSig.Params.Count);
        if (TryBuildCommandValue(method.DeclaringType, arguments) is { } commandValue)
        {
            return commandValue;
        }

        if (SystemCommandLineConstructorReaderSupport.IsOptionType(method.DeclaringType))
        {
            var optionValue = SystemCommandLineConstructorReaderSupport.TryBuildOptionValue(method.DeclaringType.ToTypeSig(), ConvertArguments(arguments));
            return optionValue is not null ? new OptionValue(optionValue.Definition) : UnknownValue.Instance;
        }

        if (SystemCommandLineConstructorReaderSupport.IsArgumentType(method.DeclaringType))
        {
            var argumentValue = SystemCommandLineConstructorReaderSupport.TryBuildArgumentValue(method.DeclaringType.ToTypeSig(), ConvertArguments(arguments));
            return argumentValue is not null ? new ArgumentValue(argumentValue.Definition) : UnknownValue.Instance;
        }

        return UnknownValue.Instance;
    }

    private static void ApplyMethodCall(
        List<MethodValue> stack,
        IMethod? method,
        IDictionary<string, StaticCommandDefinition> commands)
    {
        if (method?.MethodSig is null)
        {
            stack.Clear();
            return;
        }

        var argumentCount = method.MethodSig.Params.Count + (method.MethodSig.HasThis ? 1 : 0);
        var arguments = PopArguments(stack, argumentCount);
        if (method.MethodSig.HasThis
            && arguments.Length >= 2
            && arguments[0] is CommandValue commandValue
            && arguments[1] is OptionValue optionValue
            && IsOptionAttachMethod(method))
        {
            commandValue.UpsertOption(optionValue.Definition);
            return;
        }

        if (method.MethodSig.HasThis
            && arguments.Length >= 2
            && arguments[0] is CommandValue commandValueWithArgument
            && arguments[1] is ArgumentValue argumentValue
            && IsArgumentAttachMethod(method))
        {
            commandValueWithArgument.AddValue(argumentValue.Definition);
            return;
        }

        if (method.MethodSig.HasThis
            && arguments.Length >= 2
            && arguments[0] is CommandValue parentCommand
            && arguments[1] is CommandValue childCommand
            && IsCommandAttachMethod(method))
        {
            childCommand.AttachTo(parentCommand);
            Register(commands, childCommand);
            return;
        }

        if (!string.Equals(method.MethodSig.RetType.FullName, "System.Void", StringComparison.Ordinal)
            && ResolveReturnValue(arguments, method.MethodSig.RetType) is { } returnValue)
        {
            stack.Add(returnValue);
        }
    }

    private static MethodValue? ResolveReturnValue(IReadOnlyList<MethodValue> arguments, TypeSig returnType)
    {
        var instance = arguments.FirstOrDefault();
        return instance switch
        {
            CommandValue commandValue when IsCommandType(returnType.ToTypeDefOrRef()) => commandValue,
            OptionValue optionValue when SystemCommandLineConstructorReaderSupport.IsOptionType(returnType.ToTypeDefOrRef()) => optionValue,
            ArgumentValue argumentValue when SystemCommandLineConstructorReaderSupport.IsArgumentType(returnType.ToTypeDefOrRef()) => argumentValue,
            _ => null,
        };
    }

    private static CommandValue? TryBuildCommandValue(ITypeDefOrRef? type, IReadOnlyList<MethodValue> arguments)
    {
        if (!IsCommandType(type))
        {
            return null;
        }

        var isRoot = IsRootCommandType(type);
        var displayName = isRoot
            ? null
            : ResolveCommandName(type, arguments);
        if (!isRoot && string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var description = ResolveDescription(arguments, isRoot);
        return new CommandValue(displayName, isRoot, description);
    }

    private static string? ResolveCommandName(ITypeDefOrRef? type, IReadOnlyList<MethodValue> arguments)
    {
        var explicitName = arguments.FirstOrDefault() switch
        {
            StringValue stringValue => stringValue.Value,
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName.Trim();
        }

        var typeName = type?.Name?.ToString();
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        return typeName.EndsWith("Command", StringComparison.Ordinal)
            ? typeName[..^"Command".Length].Trim()
            : typeName.Trim();
    }

    private static string? ResolveDescription(IReadOnlyList<MethodValue> arguments, bool isRoot)
    {
        var stringValues = arguments.OfType<StringValue>()
            .Select(static value => value.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        if (stringValues.Length == 0)
        {
            return null;
        }

        return isRoot
            ? stringValues[0]
            : stringValues.Skip(1).FirstOrDefault();
    }

    private static bool IsOptionAttachMethod(IMethod method)
        => string.Equals(method.Name, "AddOption", StringComparison.Ordinal)
            || string.Equals(method.Name, "Add", StringComparison.Ordinal)
                && method.MethodSig?.Params.FirstOrDefault() is { } parameter
                && SystemCommandLineConstructorReaderSupport.IsOptionType(parameter.ToTypeDefOrRef());

    private static bool IsArgumentAttachMethod(IMethod method)
        => string.Equals(method.Name, "AddArgument", StringComparison.Ordinal)
            || string.Equals(method.Name, "Add", StringComparison.Ordinal)
                && method.MethodSig?.Params.FirstOrDefault() is { } parameter
                && SystemCommandLineConstructorReaderSupport.IsArgumentType(parameter.ToTypeDefOrRef());

    private static bool IsCommandAttachMethod(IMethod method)
        => string.Equals(method.Name, "AddCommand", StringComparison.Ordinal)
            || string.Equals(method.Name, "Add", StringComparison.Ordinal)
                && method.MethodSig?.Params.FirstOrDefault() is { } parameter
                && IsCommandType(parameter.ToTypeDefOrRef());

    private static bool IsCommandType(ITypeDefOrRef? type)
    {
        for (var current = type; current is not null;)
        {
            if (string.Equals(current.FullName, "System.CommandLine.Command", StringComparison.Ordinal)
                || string.Equals(current.FullName, "System.CommandLine.RootCommand", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.ResolveTypeDef()?.BaseType;
        }

        return false;
    }

    private static bool IsRootCommandType(ITypeDefOrRef? type)
    {
        for (var current = type; current is not null;)
        {
            if (string.Equals(current.FullName, "System.CommandLine.RootCommand", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.ResolveTypeDef()?.BaseType;
        }

        return false;
    }

    private static void Register(IDictionary<string, StaticCommandDefinition> commands, CommandValue command)
    {
        var key = command.FullKey ?? string.Empty;
        StaticCommandDefinitionSupport.UpsertBest(commands, key, command.ToDefinition());
    }

    private static SystemCommandLineConstructorReaderSupport.ConstructorValue[] ConvertArguments(IReadOnlyList<MethodValue> arguments)
    {
        var converted = new SystemCommandLineConstructorReaderSupport.ConstructorValue[arguments.Count];
        for (var index = 0; index < arguments.Count; index++)
        {
            converted[index] = arguments[index] switch
            {
                StringValue stringValue => new SystemCommandLineConstructorReaderSupport.StringValue(stringValue.Value),
                Int32Value int32Value => new SystemCommandLineConstructorReaderSupport.Int32Value(int32Value.Value),
                StringArrayValue stringArrayValue => new SystemCommandLineConstructorReaderSupport.StringArrayValue(stringArrayValue.Values),
                _ => SystemCommandLineConstructorReaderSupport.UnknownValue.Instance,
            };
        }

        return converted;
    }

    private static MethodValue[] PopArguments(List<MethodValue> stack, int count)
    {
        var values = new MethodValue[count];
        for (var index = count - 1; index >= 0; index--)
        {
            values[index] = Pop(stack);
        }

        return values;
    }

    private static MethodValue Pop(List<MethodValue> stack)
    {
        if (stack.Count == 0)
        {
            return UnknownValue.Instance;
        }

        var value = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return value;
    }

    private abstract class MethodValue;

    private sealed class StringValue(string? value) : MethodValue
    {
        public string? Value { get; } = value;
    }

    private sealed class Int32Value(int value) : MethodValue
    {
        public int Value { get; } = value;
    }

    private sealed class StringArrayValue(int length) : MethodValue
    {
        public StringArrayValue(string?[] values)
            : this(values.Length)
        {
            for (var index = 0; index < values.Length; index++)
            {
                Values[index] = values[index];
            }
        }

        public string?[] Values { get; } = new string?[length];
    }

    private sealed class OptionValue(StaticOptionDefinition definition) : MethodValue
    {
        public StaticOptionDefinition Definition { get; } = definition;
    }

    private sealed class ArgumentValue(StaticValueDefinition definition) : MethodValue
    {
        public StaticValueDefinition Definition { get; } = definition;
    }

    private sealed class CommandValue(string? displayName, bool isDefault, string? description) : MethodValue
    {
        private readonly Dictionary<string, StaticOptionDefinition> _options = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<StaticValueDefinition> _values = [];

        public string? DisplayName { get; } = displayName;

        public bool IsDefault { get; } = isDefault;

        public string? Description { get; } = description;

        public string? FullKey { get; private set; } = isDefault ? string.Empty : displayName;

        public void AttachTo(CommandValue parent)
        {
            if (IsDefault || string.IsNullOrWhiteSpace(DisplayName))
            {
                return;
            }

            FullKey = string.IsNullOrWhiteSpace(parent.FullKey)
                ? DisplayName
                : parent.FullKey + " " + DisplayName;
        }

        public void UpsertOption(StaticOptionDefinition option)
        {
            var key = option.LongName
                ?? (option.ShortName is char shortName ? shortName.ToString() : null)
                ?? "value";
            if (!_options.ContainsKey(key))
            {
                _options[key] = option;
            }
        }

        public void AddValue(StaticValueDefinition value)
        {
            var normalized = value with { Index = _values.Count };
            if (_values.Any(existing => string.Equals(existing.Name, normalized.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _values.Add(normalized);
        }

        public StaticCommandDefinition ToDefinition()
            => new(
                DisplayName,
                Description,
                IsDefault,
                IsHidden: false,
                _values.OrderBy(static value => value.Index).ToArray(),
                _options.Values.OrderBy(static option => option.LongName ?? option.ShortName?.ToString()).ToArray());
    }

    private sealed class NullValue : MethodValue
    {
        public static NullValue Instance { get; } = new();
    }

    private sealed class UnknownValue : MethodValue
    {
        public static UnknownValue Instance { get; } = new();
    }
}
