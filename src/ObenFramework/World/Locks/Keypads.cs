// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;

namespace ObenFramework.World;

/// <summary>What happened at a keypad or combination lock.</summary>
public enum KeypadEventKind
{
    /// <summary>The right code was entered (or a mod solved it): the keypad fired its success relay.</summary>
    Solved,

    /// <summary>A wrong code was entered.</summary>
    WrongCode,

    /// <summary>An open keypad was locked again, by the player at the keypad or from code.</summary>
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
/// Changes are refused (false) while a save is written or loaded.
/// </summary>
public static class Keypads
{
    /// <summary>Longest code the keypad screen takes.</summary>
    public const int MaxDigits = 4;

    /// <summary>Highest number on a rotary lock's dial (its numbers run from 1 to 32).</summary>
    public const int MaxRotaryNumber = 32;

    private static readonly AccessTools.FieldRef<Keypad, bool> OpenField = AccessTools.FieldRefAccess<Keypad, bool>("open");
    private static readonly AccessTools.FieldRef<Keypad, bool> NoPowerField = AccessTools.FieldRefAccess<Keypad, bool>("noPower");
    private static readonly AccessTools.FieldRef<Keypad, Keypad.KeypadType> TypeField = AccessTools.FieldRefAccess<Keypad, Keypad.KeypadType>("type");

    /// <summary>True when the keypad hooks are in place, so <see cref="Changed"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "World");

    public static readonly WorldEvent<KeypadEvent> Changed = new WorldEvent<KeypadEvent>(nameof(Changed));

    public static List<Keypad> All() => ObjectIds.Active<Keypad>();

    public static Keypad Find(string id) => ObjectIds.Find<Keypad>(id);

    public static string IdOf(Keypad keypad) => ObjectIds.IdOf(keypad);

    public static bool IsOpen(Keypad keypad) => keypad != null && OpenField(keypad);

    /// <summary>A rotary combination lock (a dial with the numbers 1 to 32) rather than a keypad (digits 0 to 9).</summary>
    public static bool IsRotaryLock(Keypad keypad) => keypad != null && TypeField(keypad) == Keypad.KeypadType.RotaryLock;

    /// <summary>A keypad without power (or a blocked rotary lock) refuses every code.</summary>
    public static bool HasPower(Keypad keypad) => keypad != null && !NoPowerField(keypad);

    /// <summary>A copy of the keypad's code, one digit per entry.</summary>
    public static int[] CodeOf(Keypad keypad)
    {
        if (keypad == null)
            throw new ArgumentNullException(nameof(keypad));
        return keypad.code != null ? (int[])keypad.code.Clone() : Array.Empty<int>();
    }

    /// <summary>
    /// Sets the code: 1 to <see cref="MaxDigits"/> entries, each a digit 0 to 9 on a keypad, or a number 1 to
    /// <see cref="MaxRotaryNumber"/> on a rotary lock.
    /// </summary>
    public static bool SetCode(Keypad keypad, int[] code)
    {
        if (keypad == null)
            throw new ArgumentNullException(nameof(keypad));
        if (code == null || code.Length == 0 || code.Length > MaxDigits)
            throw new ArgumentException($"A code has 1 to {MaxDigits} entries.", nameof(code));
        bool rotary = IsRotaryLock(keypad);
        int lowest = rotary ? 1 : 0, highest = rotary ? MaxRotaryNumber : 9;
        foreach (int digit in code)
        {
            if (digit < lowest || digit > highest)
                throw new ArgumentException($"Each entry must be {lowest} to {highest} on this {(rotary ? "rotary lock" : "keypad")}.", nameof(code));
        }
        if (SaveGate.IsBusy)
            return false;
        keypad.code = (int[])code.Clone();
        return true;
    }

    /// <summary>
    /// Opens the keypad as if the right code was entered (its success sound and relay, which usually open a
    /// door), or locks it again with its lock relay.
    /// </summary>
    public static bool SetOpen(Keypad keypad, bool open)
    {
        if (keypad == null)
            throw new ArgumentNullException(nameof(keypad));
        if (IsOpen(keypad) == open)
            return true;
        if (SaveGate.IsBusy)
            return false;
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
        return IsOpen(keypad) == open;
    }

    /// <summary>
    /// Plays the keypad's wrong-code reaction (its failure relay: a buzz, a light, an alarm in some places),
    /// as if a wrong code was entered. Raises <c>WrongCode</c> like a real attempt does.
    /// </summary>
    public static bool Refuse(Keypad keypad)
    {
        if (keypad == null)
            throw new ArgumentNullException(nameof(keypad));
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            keypad.TriggerFailure();
        return true;
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

    // The player locking an open keypad by hand goes through its private Use, not Lock.
    [HarmonyPrefix]
    [HarmonyPatch("Use")]
    private static void BeforeUse(bool ___open, out bool __state) => __state = ___open;

    [HarmonyPostfix]
    [HarmonyPatch("Use")]
    private static void AfterUse(Keypad __instance, bool ___open, bool __state)
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
