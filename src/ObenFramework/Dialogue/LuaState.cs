using Language.Lua;
using ObenFramework.Game;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>
/// Whether the Dialogue System's Lua variables can be trusted right now. A save load throws the
/// <c>Variable</c> and <c>Actor</c> tables away, refills them from the save and applies the saved text as
/// its last step, so anything written earlier in the load is lost.
/// </summary>
public static class LuaState
{
    // Set when the game announces a load, cleared once the load finished. The framework's own
    // WorldState.IsLoadingSave only starts a bit later, after the scene was swapped.
    private static bool _loadAnnounced;

    /// <summary>The Lua tables the variables live in exist (the dialogue database has been loaded).</summary>
    public static bool IsReady
    {
        get
        {
            var env = Lua.Environment;
            return env.GetValue("Variable") is LuaTable && env.GetValue("Actor") is LuaTable;
        }
    }

    /// <summary>A save is loading. Reads give the old state and writes get replaced by the save's.</summary>
    public static bool IsLoading => _loadAnnounced || WorldState.IsLoadingSave;

    /// <summary>Variables and fields can be written and will stay.</summary>
    public static bool CanWrite => IsReady && !IsLoading;

    internal static void LoadAnnounced() => _loadAnnounced = true;

    internal static void LoadFinished()
    {
        _loadAnnounced = false;
        LuaWrites.Flush();
    }

    internal static void Settled()
    {
        if (!IsLoading)
            LuaWrites.Flush();
    }
}
