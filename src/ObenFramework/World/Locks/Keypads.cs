// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;

namespace ObenFramework.World;

/// <summary>What happened at a keypad or combination lock.</summary>
public enum KeypadEventKind
{
    /// <summary>The right code was entered (or a mod solved it): the keypad fired its success relay.</summary>
    Solved,

    /// <summary>A wrong code was entered.</summary>
    WrongCode,

    /// <summary>An open keypad was locked again.</summary>
    Locked,
}

public readonly struct KeypadEvent
{
    public KeypadEvent(Keypad keypad, KeypadEventKind kind, ChangeSource source)
    {
        Keypad = keypad;
        Kind = kind;
        Source = source;
    }

    public Keypad Keypad { get; }

    public KeypadEventKind Kind { get; }

    public ChangeSource Source { get; }

    public string Id => Keypads.IdOf(Keypad);
}

/// <summary>
/// Keypads and rotary combination locks: their codes, whether they are open, and solving or locking them from
/// code. The code is part of the level's design, so a changed code lasts until the level is loaded again.
/// </summary>
public static class Keypads
{
    /// <summary>Longest code the keypad screen takes.</summary>
    public const int MaxDigits = 4;

    private static readonly AccessTools.FieldRef<Keypad, bool> OpenField = AccessTools.FieldRefAccess<Keypad, bool>("open");
    private static readonly AccessTools.FieldRef<Keypad, bool> NoPowerField = AccessTools.FieldRefAccess<Keypad, bool>("noPower");

    public static readonly WorldEvent<KeypadEvent> Changed = new WorldEvent<KeypadEvent>(nameof(Changed));

    public static List<Keypad> All() => ObjectIds.Active<Keypad>();

    public static Keypad Find(string id) => ObjectIds.Find<Keypad>(id);

    public static string IdOf(Keypad keypad) => ObjectIds.IdOf(keypad);

    public static bool IsOpen(Keypad keypad) => keypad != null && OpenField(keypad);

    /// <summary>A keypad without power (or a blocked rotary lock) refuses every code.</summary>
    public static bool HasPower(Keypad keypad) => keypad != null && !NoPowerField(keypad);

    /// <summary>A copy of the keypad's code, one digit per entry.</summary>
    public static int[] CodeOf(Keypad keypad)
    {
        if (keypad == null)
            throw new ArgumentNullException(nameof(keypad));
        return keypad.code != null ? (int[])keypad.code.Clone() : Array.Empty<int>();
    }

    /// <summary>Sets the code: 1 to <see cref="MaxDigits"/> digits, each 0 to 9.</summary>
    public static void SetCode(Keypad keypad, int[] code)
    {
        if (keypad == null)
            throw new ArgumentNullException(nameof(keypad));
        if (code == null || code.Length == 0 || code.Length > MaxDigits)
            throw new ArgumentException($"A code has 1 to {MaxDigits} digits.", nameof(code));
        foreach (int digit in code)
        {
            if (digit < 0 || digit > 9)
                throw new ArgumentException("Each digit must be 0 to 9.", nameof(code));
        }
        keypad.code = (int[])code.Clone();
    }

    /// <summary>
    /// Opens the keypad as if the right code was entered (its success sound and relay, which usually open a
    /// door), or locks it again with its lock relay.
    /// </summary>
    public static void SetOpen(Keypad keypad, bool open)
    {
        if (keypad == null)
            throw new ArgumentNullException(nameof(keypad));
        if (IsOpen(keypad) == open)
            return;
        using (WorldChanges.EnterMod())
        {
            if (open)
            {
                keypad.TriggerSuccess();
                // Keypads that don't stay open on success still count as open from the mod's point of view.
                keypad.SetOpen(true);
            }
            else
            {
                keypad.Lock();
            }
        }
    }
}

[HarmonyPatch(typeof(Keypad))]
internal static class KeypadPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(Keypad.TriggerSuccess))]
    private static void Solved(Keypad __instance) => Raise(__instance, KeypadEventKind.Solved);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Keypad.TriggerFailure))]
    private static void Wrong(Keypad __instance) => Raise(__instance, KeypadEventKind.WrongCode);

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Keypad.Lock))]
    private static void BeforeLock(bool ___open, out bool __state) => __state = ___open;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Keypad.Lock))]
    private static void AfterLock(Keypad __instance, bool ___open, bool __state)
    {
        if (__state && !___open)
            Raise(__instance, KeypadEventKind.Locked);
    }

    private static void Raise(Keypad keypad, KeypadEventKind kind)
    {
        try
        {
            if (Keypads.Changed.HasSubscribers && WorldChanges.Active)
                Keypads.Changed.Raise(new KeypadEvent(keypad, kind, WorldChanges.Current));
        }
        catch (Exception e) { WorldLog.Failed("Keypad hook", e); }
    }
}
