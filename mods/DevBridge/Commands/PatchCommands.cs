using System;
using System.Reflection;
using System.Runtime.InteropServices;
using DevBridge.Server;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

/// <summary>
/// Checks the jump Harmony writes at the start of every patched method. After many hot reloads
/// this is how a dead jump shows up before the game runs into it.
/// </summary>
internal static class PatchCommands
{
    private const uint MemCommit = 0x1000;
    private const uint PageNoAccess = 0x01;
    private const uint PageGuard = 0x100;

    [BridgeCommand("patches.check", "Every patched method's jump, and the ones pointing at memory that's gone.", Params = new[] { "all?" })]
    private static object Check(JObject p)
    {
        bool all = p.Bool("all", false);
        var methods = new JArray();
        var broken = new JArray();
        int total = 0, indirect = 0, absolute = 0, direct = 0, other = 0;

        foreach (var method in Harmony.GetAllPatchedMethods())
        {
            total++;
            var jump = Inspect(method);
            if (jump.Kind == "indirect") indirect++;
            else if (jump.Kind == "absolute") absolute++;
            else if (jump.Kind == "direct") direct++;
            else other++;

            var info = Harmony.GetPatchInfo(method);
            var row = new JObject
            {
                ["method"] = method.DeclaringType?.FullName + "." + method.Name,
                ["owners"] = info == null ? new JArray() : new JArray(info.Owners),
                ["kind"] = jump.Kind,
                ["cell"] = jump.Cell == 0 ? null : jump.Cell.ToString("x"),
                ["target"] = jump.Target == 0 ? null : jump.Target.ToString("x"),
            };
            if (jump.Problem != null)
            {
                row["problem"] = jump.Problem;
                row["memory"] = jump.Memory;
                broken.Add(row);
            }
            if (all)
                methods.Add(row);
        }

        var result = new JObject
        {
            ["patched"] = total,
            ["indirect"] = indirect,
            ["absolute"] = absolute,
            ["direct"] = direct,
            ["other"] = other,
            ["broken"] = broken,
        };
        if (all)
            result["methods"] = methods;
        return result;
    }

    private struct Jump
    {
        public string Kind;
        public long Cell;
        public long Target;
        public string Problem;
        public string Memory;
    }

    private static Jump Inspect(MethodBase method)
    {
        var jump = new Jump { Kind = "other" };
        IntPtr start = method.MethodHandle.GetFunctionPointer();
        if (!Readable(start.ToInt64()))
        {
            jump.Problem = "method start is not readable";
            return jump;
        }

        byte b0 = Marshal.ReadByte(start), b1 = Marshal.ReadByte(start, 1);
        if (b0 == 0xE9)
        {
            // jmp rel32
            jump.Kind = "direct";
            jump.Target = start.ToInt64() + 5 + Marshal.ReadInt32(start, 1);
        }
        else if (b0 == 0xFF && b1 == 0x25)
        {
            // jmp [rip+disp32]: the target sits in an 8-byte cell, right after the
            // instruction when disp32 is 0 (absolute), elsewhere when it isn't (indirect)
            int disp = Marshal.ReadInt32(start, 2);
            jump.Kind = disp == 0 ? "absolute" : "indirect";
            jump.Cell = start.ToInt64() + 6 + disp;
            if (!Readable(jump.Cell))
            {
                jump.Problem = "jump cell memory was released";
                jump.Memory = Describe(jump.Cell);
                return jump;
            }
            jump.Target = Marshal.ReadInt64(new IntPtr(jump.Cell));
        }
        else
        {
            return jump;
        }

        if (!Readable(jump.Target))
        {
            jump.Problem = "jump target memory was released";
            jump.Memory = Describe(jump.Target);
        }
        return jump;
    }

    // State, protection and the allocation the address belongs to, as Windows reports them.
    private static string Describe(long address)
    {
        if (VirtualQuery(new IntPtr(address), out var info, (UIntPtr)Marshal.SizeOf<MemoryInfo>()) == UIntPtr.Zero)
            return "unknown";
        return $"state {info.State:x} protect {info.Protect:x} allocation {info.AllocationBase.ToInt64():x} region {info.BaseAddress.ToInt64():x}+{info.RegionSize.ToUInt64():x}";
    }

    private static bool Readable(long address)
    {
        if (VirtualQuery(new IntPtr(address), out var info, (UIntPtr)Marshal.SizeOf<MemoryInfo>()) == UIntPtr.Zero)
            return false;
        return info.State == MemCommit && (info.Protect & (PageNoAccess | PageGuard)) == 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public UIntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll")]
    private static extern UIntPtr VirtualQuery(IntPtr address, out MemoryInfo buffer, UIntPtr length);
}
