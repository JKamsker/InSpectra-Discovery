namespace InSpectra.Discovery.Tool.StaticAnalysis.Attributes;

using InSpectra.Discovery.Tool.StaticAnalysis.Inspection;
using InSpectra.Discovery.Tool.StaticAnalysis.Models;

using dnlib.DotNet;
using dnlib.DotNet.Emit;

internal static class SystemCommandLineConstructorReaderSupport
{
    public static (IReadOnlyList<StaticOptionDefinition> Options, IReadOnlyList<StaticValueDefinition> Values) ReadSurface(TypeDef typeDef)
    {
        var options = new Dictionary<string, StaticOptionDefinition>(StringComparer.OrdinalIgnoreCase);
        var values = new List<StaticValueDefinition>();

        foreach (var constructor in typeDef.Methods.Where(static method => method.IsInstanceConstructor && method.HasBody))
        {
            ReadConstructorSurface(constructor, options, values);
        }

        return (
            options.Values.OrderBy(static option => option.LongName ?? option.ShortName?.ToString()).ToArray(),
            values.OrderBy(static value => value.Index).ToArray());
    }

    private static void ReadConstructorSurface(
        MethodDef constructor,
        IDictionary<string, StaticOptionDefinition> options,
        ICollection<StaticValueDefinition> values)
    {
        var stack = new List<ConstructorValue>();
        var locals = new Dictionary<int, ConstructorValue>();

        foreach (var instruction in constructor.Body.Instructions)
        {
            if (TryReadInt32(instruction, out var intValue))
            {
                stack.Add(new Int32Value(intValue));
                continue;
            }

            switch (instruction.OpCode.Code)
            {
                case Code.Nop:
                case Code.Ret:
                    break;
                case Code.Ldarg_0:
                    stack.Add(CurrentCommandValue.Instance);
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
                    if (TryGetLocalIndex(instruction, out var storeIndex))
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
                    if (TryGetLocalIndex(instruction, out var loadIndex))
                    {
                        stack.Add(locals.TryGetValue(loadIndex, out var value) ? value : UnknownValue.Instance);
                    }

                    break;
                case Code.Newobj:
                    stack.Add(BuildConstructedValue(stack, instruction.Operand as IMethod));
                    break;
                case Code.Call:
                case Code.Callvirt:
                    ApplyMethodCall(stack, instruction.Operand as IMethod, options, values);
                    break;
                default:
                    stack.Clear();
                    break;
            }
        }
    }

    private static ConstructorValue BuildArrayValue(List<ConstructorValue> stack, ITypeDefOrRef? elementType)
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

    private static void ApplyArrayElementAssignment(List<ConstructorValue> stack)
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

    private static ConstructorValue BuildConstructedValue(List<ConstructorValue> stack, IMethod? method)
    {
        if (method?.MethodSig is null)
        {
            return UnknownValue.Instance;
        }

        var arguments = PopArguments(stack, method.MethodSig.Params.Count);
        if (IsOptionType(method.DeclaringType))
        {
            var optionValue = TryBuildOptionValue(method.DeclaringType.ToTypeSig(), arguments);
            return optionValue is not null ? optionValue : UnknownValue.Instance;
        }

        if (IsArgumentType(method.DeclaringType))
        {
            var argumentValue = TryBuildArgumentValue(method.DeclaringType.ToTypeSig(), arguments);
            return argumentValue is not null ? argumentValue : UnknownValue.Instance;
        }

        return UnknownValue.Instance;
    }

    private static void ApplyMethodCall(
        List<ConstructorValue> stack,
        IMethod? method,
        IDictionary<string, StaticOptionDefinition> options,
        ICollection<StaticValueDefinition> values)
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
            && arguments[0] is CurrentCommandValue
            && arguments[1] is OptionValue optionValue
            && IsOptionAttachMethod(method))
        {
            UpsertOption(options, optionValue.Definition);
            return;
        }

        if (method.MethodSig.HasThis
            && arguments.Length >= 2
            && arguments[0] is CurrentCommandValue
            && arguments[1] is ArgumentValue argumentValue
            && IsArgumentAttachMethod(method))
        {
            values.Add(argumentValue.Definition with { Index = values.Count });
            return;
        }

        if (!string.Equals(method.MethodSig.RetType.FullName, "System.Void", StringComparison.Ordinal))
        {
            stack.Add(ResolveReturnValue(arguments, method.MethodSig.RetType));
        }
    }

    private static ConstructorValue ResolveReturnValue(IReadOnlyList<ConstructorValue> arguments, TypeSig returnType)
    {
        var instance = arguments.FirstOrDefault();
        return instance switch
        {
            OptionValue optionValue when IsOptionType(returnType.ToTypeDefOrRef()) => optionValue,
            ArgumentValue argumentValue when IsArgumentType(returnType.ToTypeDefOrRef()) => argumentValue,
            _ => UnknownValue.Instance,
        };
    }

    private static void UpsertOption(IDictionary<string, StaticOptionDefinition> options, StaticOptionDefinition definition)
    {
        var key = definition.LongName
            ?? (definition.ShortName is char shortName ? shortName.ToString() : null)
            ?? "value";
        if (!options.ContainsKey(key))
        {
            options[key] = definition;
        }
    }

    private static OptionValue? TryBuildOptionValue(TypeSig? typeSig, IReadOnlyList<ConstructorValue> arguments)
    {
        var (longName, shortName) = ReadOptionAliases(arguments.FirstOrDefault());
        if (longName is null && shortName is null)
        {
            return null;
        }

        var valueType = ExtractGenericArgument(typeSig);
        return new OptionValue(
            new StaticOptionDefinition(
                LongName: longName,
                ShortName: shortName,
                IsRequired: false,
                IsSequence: StaticAnalysisTypeSupport.IsSequenceType(valueType),
                IsBoolLike: StaticAnalysisTypeSupport.IsBoolType(valueType),
                ClrType: StaticAnalysisTypeSupport.GetClrTypeName(valueType),
                Description: arguments.Skip(1).OfType<StringValue>().Select(static value => value.Value).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
                DefaultValue: null,
                MetaValue: null,
                AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(valueType),
                PropertyName: null));
    }

    private static ArgumentValue? TryBuildArgumentValue(TypeSig? typeSig, IReadOnlyList<ConstructorValue> arguments)
    {
        var rawName = arguments.FirstOrDefault() switch
        {
            StringValue stringValue => stringValue.Value,
            StringArrayValue arrayValue => arrayValue.Values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
            _ => null,
        };
        var name = NormalizeArgumentName(rawName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var valueType = ExtractGenericArgument(typeSig);
        return new ArgumentValue(
            new StaticValueDefinition(
                Index: 0,
                Name: name,
                IsRequired: true,
                IsSequence: StaticAnalysisTypeSupport.IsSequenceType(valueType),
                ClrType: StaticAnalysisTypeSupport.GetClrTypeName(valueType),
                Description: arguments.Skip(1).OfType<StringValue>().Select(static value => value.Value).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
                DefaultValue: null,
                AcceptedValues: StaticAnalysisTypeSupport.GetAcceptedValues(valueType)));
    }

    private static (string? LongName, char? ShortName) ReadOptionAliases(ConstructorValue? value)
    {
        var aliases = value switch
        {
            StringValue stringValue => [stringValue.Value],
            StringArrayValue arrayValue => arrayValue.Values,
            _ => [],
        };
        var longName = aliases
            .FirstOrDefault(static alias => !string.IsNullOrWhiteSpace(alias) && alias.StartsWith("--", StringComparison.Ordinal))
            ?? aliases.FirstOrDefault(static alias => !string.IsNullOrWhiteSpace(alias) && !alias.StartsWith("-", StringComparison.Ordinal));
        var shortAlias = aliases.FirstOrDefault(static alias => !string.IsNullOrWhiteSpace(alias) && alias.Length == 2 && alias[0] == '-');
        return (NormalizeOptionName(longName), shortAlias is { Length: 2 } ? shortAlias[1] : null);
    }

    private static string? NormalizeOptionName(string? alias)
        => string.IsNullOrWhiteSpace(alias)
            ? null
            : alias.Trim().TrimStart('-', '/');

    private static string? NormalizeArgumentName(string? name)
        => string.IsNullOrWhiteSpace(name)
            ? null
            : name.Trim().Trim('<', '>', '[', ']', '(', ')');

    private static bool IsOptionAttachMethod(IMethod method)
        => string.Equals(method.Name, "AddOption", StringComparison.Ordinal)
            || string.Equals(method.Name, "Add", StringComparison.Ordinal)
                && method.MethodSig?.Params.FirstOrDefault() is { } parameter
                && IsOptionType(parameter.ToTypeDefOrRef());

    private static bool IsArgumentAttachMethod(IMethod method)
        => string.Equals(method.Name, "AddArgument", StringComparison.Ordinal)
            || string.Equals(method.Name, "Add", StringComparison.Ordinal)
                && method.MethodSig?.Params.FirstOrDefault() is { } parameter
                && IsArgumentType(parameter.ToTypeDefOrRef());

    private static TypeSig? ExtractGenericArgument(TypeSig? typeSig)
        => typeSig is GenericInstSig genericInstSig && genericInstSig.GenericArguments.Count > 0
            ? genericInstSig.GenericArguments[0]
            : null;

    private static bool IsOptionType(ITypeDefOrRef? type)
        => type?.FullName.StartsWith("System.CommandLine.Option", StringComparison.Ordinal) == true;

    private static bool IsArgumentType(ITypeDefOrRef? type)
        => type?.FullName.StartsWith("System.CommandLine.Argument", StringComparison.Ordinal) == true;

    private static bool TryGetLocalIndex(Instruction instruction, out int index)
    {
        index = instruction.OpCode.Code switch
        {
            Code.Ldloc_0 or Code.Stloc_0 => 0,
            Code.Ldloc_1 or Code.Stloc_1 => 1,
            Code.Ldloc_2 or Code.Stloc_2 => 2,
            Code.Ldloc_3 or Code.Stloc_3 => 3,
            _ => -1,
        };
        if (index >= 0)
        {
            return true;
        }

        if (instruction.Operand is Local local)
        {
            index = local.Index;
            return true;
        }

        index = -1;
        return false;
    }

    private static bool TryReadInt32(Instruction instruction, out int value)
    {
        value = instruction.OpCode.Code switch
        {
            Code.Ldc_I4_M1 => -1,
            Code.Ldc_I4_0 => 0,
            Code.Ldc_I4_1 => 1,
            Code.Ldc_I4_2 => 2,
            Code.Ldc_I4_3 => 3,
            Code.Ldc_I4_4 => 4,
            Code.Ldc_I4_5 => 5,
            Code.Ldc_I4_6 => 6,
            Code.Ldc_I4_7 => 7,
            Code.Ldc_I4_8 => 8,
            Code.Ldc_I4_S => (sbyte)instruction.Operand,
            Code.Ldc_I4 => (int)instruction.Operand,
            _ => 0,
        };
        return instruction.OpCode.Code is >= Code.Ldc_I4_M1 and <= Code.Ldc_I4
            || instruction.OpCode.Code == Code.Ldc_I4_S;
    }

    private static ConstructorValue[] PopArguments(List<ConstructorValue> stack, int count)
    {
        var values = new ConstructorValue[count];
        for (var index = count - 1; index >= 0; index--)
        {
            values[index] = Pop(stack);
        }

        return values;
    }

    private static ConstructorValue Pop(List<ConstructorValue> stack)
    {
        if (stack.Count == 0)
        {
            return UnknownValue.Instance;
        }

        var value = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return value;
    }

    private abstract record ConstructorValue;

    private sealed record StringValue(string? Value) : ConstructorValue;

    private sealed record Int32Value(int Value) : ConstructorValue;

    private sealed record StringArrayValue(int Length) : ConstructorValue
    {
        public string?[] Values { get; } = new string?[Length];
    }

    private sealed record OptionValue(StaticOptionDefinition Definition) : ConstructorValue;

    private sealed record ArgumentValue(StaticValueDefinition Definition) : ConstructorValue;

    private sealed record CurrentCommandValue : ConstructorValue
    {
        public static CurrentCommandValue Instance { get; } = new();
    }

    private sealed record NullValue : ConstructorValue
    {
        public static NullValue Instance { get; } = new();
    }

    private sealed record UnknownValue : ConstructorValue
    {
        public static UnknownValue Instance { get; } = new();
    }
}
