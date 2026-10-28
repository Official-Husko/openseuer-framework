using System;
using System.Collections.Generic;

namespace ObenFramework.Saves;

/// <summary>
/// The callback phases of <c>SavableScript</c> that move an object's state in and out of the
/// game's save lists, in the order the game runs them. Left out on purpose: the destructible list
/// phases (they spawn or destroy objects), <c>OnSavingFile</c> (writes side files) and the map
/// change phases.
/// </summary>
[Flags]
internal enum SavePhases
{
    None = 0,
    Primary = 1,
    Secondary = 2,
    Tertiary = 4,
    Special = 8,
    Main = 16,
    Late = 32,
    /// <summary>What the game runs on objects in the scene (the Special phase is for its extra root list).</summary>
    Scene = Primary | Secondary | Tertiary | Main | Late,
    All = Scene | Special,
}

/// <summary>Runs single phases on a component and knows which phases a type actually overrides.</summary>
internal static class SavePhaseTable
{
    public static readonly SavePhases[] Order =
    {
        SavePhases.Primary, SavePhases.Secondary, SavePhases.Tertiary,
        SavePhases.Special, SavePhases.Main, SavePhases.Late,
    };

    private static readonly string[] SaveNames =
    {
        "OnSavingGamePrimary", "OnSavingGameSecondary", "OnSavingGameTertiary",
        "OnSavingGameSpecial", "OnSavingGame", "OnSavingGameLatePrimary",
    };

    private static readonly string[] LoadNames =
    {
        "OnLoadingGamePrimary", "OnLoadingGameSecondary", "OnLoadingGameTertiary",
        "OnLoadingGameSpecial", "OnLoadingGame", "OnLoadingGameLatePrimary",
    };

    private static readonly Dictionary<Type, SavePhases> SaveMasks = new Dictionary<Type, SavePhases>();
    private static readonly Dictionary<Type, SavePhases> LoadMasks = new Dictionary<Type, SavePhases>();

    /// <summary>Phases of <paramref name="type"/> that do something, i.e. are overridden from SavableScript's no-ops.</summary>
    public static SavePhases Overridden(Type type, bool saving)
    {
        var cache = saving ? SaveMasks : LoadMasks;
        if (cache.TryGetValue(type, out var mask))
            return mask;

        var names = saving ? SaveNames : LoadNames;
        for (int i = 0; i < names.Length; i++)
        {
            var method = type.GetMethod(names[i], Type.EmptyTypes);
            if (method != null && method.DeclaringType != typeof(SavableScript))
                mask |= Order[i];
        }
        cache[type] = mask;
        return mask;
    }

    public static bool RunSave(SavableScript script, SavePhases phase)
    {
        switch (phase)
        {
            case SavePhases.Primary: return script.OnSavingGamePrimary();
            case SavePhases.Secondary: return script.OnSavingGameSecondary();
            case SavePhases.Tertiary: return script.OnSavingGameTertiary();
            case SavePhases.Special: return script.OnSavingGameSpecial();
            case SavePhases.Main: return script.OnSavingGame();
            case SavePhases.Late: return script.OnSavingGameLatePrimary();
            default: return true;
        }
    }

    public static bool RunLoad(SavableScript script, SavePhases phase)
    {
        switch (phase)
        {
            case SavePhases.Primary: return script.OnLoadingGamePrimary();
            case SavePhases.Secondary: return script.OnLoadingGameSecondary();
            case SavePhases.Tertiary: return script.OnLoadingGameTertiary();
            case SavePhases.Special: return script.OnLoadingGameSpecial();
            case SavePhases.Main: return script.OnLoadingGame();
            case SavePhases.Late: return script.OnLoadingGameLatePrimary();
            default: return true;
        }
    }

    public static void Clear()
    {
        SaveMasks.Clear();
        LoadMasks.Clear();
    }
}
