// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using System.Reflection;

namespace ObenLoader;

/// <summary>A patch that went on, or failed to: who put it there, on what, and with which handlers.</summary>
public sealed class PatchReport
{
    public PatchReport(string owner, string target, MethodBase original, MethodInfo prefix, MethodInfo postfix,
        MethodInfo transpiler, MethodInfo finalizer, string error = null)
    {
        Owner = owner;
        Target = target;
        Original = original;
        Prefix = prefix;
        Postfix = postfix;
        Transpiler = transpiler;
        Finalizer = finalizer;
        Error = error;
    }

    /// <summary>A failure before any method was patched, e.g. a missing method or a broken patch class.</summary>
    public static PatchReport Failed(string owner, string target, string error) =>
        new PatchReport(owner, target, null, null, null, null, null, error ?? "unknown error");

    /// <summary>The Harmony id that patched: a mod's id for the mod's own patches.</summary>
    public string Owner { get; }

    /// <summary>"Type.Method", or the patch class when the class failed before a method was found.</summary>
    public string Target { get; }

    /// <summary>The patched method, or null when it wasn't found.</summary>
    public MethodBase Original { get; }

    public MethodInfo Prefix { get; }
    public MethodInfo Postfix { get; }
    public MethodInfo Transpiler { get; }
    public MethodInfo Finalizer { get; }

    /// <summary>Why the patch failed, or null when it went on.</summary>
    public string Error { get; }

    public bool Ok => Error == null;

    /// <summary>The handlers that are set, in prefix, postfix, transpiler, finalizer order.</summary>
    public List<MethodInfo> Handlers
    {
        get
        {
            var list = new List<MethodInfo>(4);
            foreach (var handler in new[] { Prefix, Postfix, Transpiler, Finalizer })
            {
                if (handler != null)
                    list.Add(handler);
            }
            return list;
        }
    }

    /// <summary>"prefix,postfix" and so on.</summary>
    public string Kinds
    {
        get
        {
            var kinds = new List<string>(4);
            if (Prefix != null) kinds.Add("prefix");
            if (Postfix != null) kinds.Add("postfix");
            if (Transpiler != null) kinds.Add("transpiler");
            if (Finalizer != null) kinds.Add("finalizer");
            return string.Join(",", kinds.ToArray());
        }
    }

    public override string ToString() => Ok
        ? $"{Owner} patched {Target} ({Kinds})"
        : $"{Owner} failed to patch {Target}: {Error}";
}
