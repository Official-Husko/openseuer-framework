using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ObenTool.Checks;

/// <summary>
/// Works out which instructions produced each argument of a call, so constants such as typeof(X) and
/// "name" can be read back out of a method body. Only straight-line code is followed: an argument
/// that comes from a branch, a local or a field is "not a constant" and the call is skipped.
/// </summary>
public static class IlArgs
{
    /// <summary>One slice of instructions per argument (the receiver first for instance calls), or null if they can't be told apart.</summary>
    public static Instruction[][]? Slices(Mono.Collections.Generic.Collection<Instruction> body, int callIndex)
    {
        if (body[callIndex].Operand is not MethodReference method)
            return null;
        int count = method.Parameters.Count + (method.HasThis && !method.ExplicitThis ? 1 : 0);
        var targets = BranchTargets(body);
        var slices = new Instruction[count][];
        int end = callIndex - 1;
        for (int arg = count - 1; arg >= 0; arg--)
        {
            int need = 1;
            int start = end;
            for (; start >= 0; start--)
            {
                var ins = body[start];
                if (ins.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Return or FlowControl.Throw)
                    return null;
                int pops = Pops(ins), pushes = Pushes(ins);
                if (pops < 0 || pushes < 0)
                    return null;
                need += pops - pushes;
                if (need == 0)
                    break;
                if (need < 0 || targets.Contains(ins))
                    return null;
            }
            if (start < 0)
                return null;
            slices[arg] = body.Skip(start).Take(end - start + 1).ToArray();
            end = start - 1;
        }
        return slices;
    }

    /// <summary>The value of a string constant argument.</summary>
    public static string? AsString(Instruction[] slice) =>
        slice.Length == 1 && slice[0].OpCode == OpCodes.Ldstr ? (string)slice[0].Operand : null;

    /// <summary>The type of a typeof(X) argument.</summary>
    public static TypeReference? AsType(Instruction[] slice) =>
        slice.Length == 2 && slice[0].OpCode == OpCodes.Ldtoken && slice[0].Operand is TypeReference type && IsTypeFromHandle(slice[1])
            ? type
            : null;

    /// <summary>The element types of a new[] { typeof(A), typeof(B) } argument; null for anything else (also for a null argument).</summary>
    public static List<TypeReference>? AsTypeArray(Instruction[] slice)
    {
        if (slice.Length < 2 || slice[1].OpCode != OpCodes.Newarr)
        {
            bool empty = slice.Length == 1 && (slice[0].Operand is FieldReference { Name: "EmptyTypes" }
                || slice[0].Operand is MethodReference { Name: "Empty" });
            return empty ? new List<TypeReference>() : null;
        }

        var types = new List<TypeReference>();
        for (int i = 2; i < slice.Length; i++)
        {
            if (slice[i].OpCode == OpCodes.Ldtoken && slice[i].Operand is TypeReference t && i + 1 < slice.Length && IsTypeFromHandle(slice[i + 1]))
                types.Add(t);
            else if (slice[i].OpCode == OpCodes.Ldtoken)
                return null;
        }
        return types.Count == Count(slice[0]) ? types : null;
    }

    private static int Count(Instruction ldc) => ldc.OpCode.Code switch
    {
        Code.Ldc_I4 => (int)ldc.Operand,
        Code.Ldc_I4_S => (sbyte)ldc.Operand,
        >= Code.Ldc_I4_0 and <= Code.Ldc_I4_8 => ldc.OpCode.Code - Code.Ldc_I4_0,
        _ => -1,
    };

    private static bool IsTypeFromHandle(Instruction ins) =>
        ins.Operand is MethodReference { Name: "GetTypeFromHandle" };

    private static HashSet<Instruction> BranchTargets(Mono.Collections.Generic.Collection<Instruction> body)
    {
        var set = new HashSet<Instruction>();
        foreach (var ins in body)
        {
            if (ins.Operand is Instruction target)
                set.Add(target);
            else if (ins.Operand is Instruction[] many)
                set.UnionWith(many);
        }
        return set;
    }

    private static int Pops(Instruction ins)
    {
        switch (ins.OpCode.StackBehaviourPop)
        {
            case StackBehaviour.Pop0: return 0;
            case StackBehaviour.Pop1:
            case StackBehaviour.Popi:
            case StackBehaviour.Popref:
                return 1;
            case StackBehaviour.Pop1_pop1:
            case StackBehaviour.Popi_pop1:
            case StackBehaviour.Popi_popi:
            case StackBehaviour.Popi_popi8:
            case StackBehaviour.Popi_popr4:
            case StackBehaviour.Popi_popr8:
            case StackBehaviour.Popref_pop1:
            case StackBehaviour.Popref_popi:
                return 2;
            case StackBehaviour.Popi_popi_popi:
            case StackBehaviour.Popref_popi_popi:
            case StackBehaviour.Popref_popi_popi8:
            case StackBehaviour.Popref_popi_popr4:
            case StackBehaviour.Popref_popi_popr8:
            case StackBehaviour.Popref_popi_popref:
                return 3;
            case StackBehaviour.Varpop:
                return ins.Operand is MethodReference m
                    ? m.Parameters.Count + (m.HasThis && !m.ExplicitThis && ins.OpCode != OpCodes.Newobj ? 1 : 0)
                    : -1;
            default: return -1;
        }
    }

    private static int Pushes(Instruction ins)
    {
        switch (ins.OpCode.StackBehaviourPush)
        {
            case StackBehaviour.Push0: return 0;
            case StackBehaviour.Push1:
            case StackBehaviour.Pushi:
            case StackBehaviour.Pushi8:
            case StackBehaviour.Pushr4:
            case StackBehaviour.Pushr8:
            case StackBehaviour.Pushref:
                return 1;
            case StackBehaviour.Push1_push1: return 2;
            case StackBehaviour.Varpush:
                return ins.Operand is MethodReference m
                    ? (ins.OpCode == OpCodes.Newobj || m.ReturnType.MetadataType != MetadataType.Void ? 1 : 0)
                    : -1;
            default: return -1;
        }
    }
}
