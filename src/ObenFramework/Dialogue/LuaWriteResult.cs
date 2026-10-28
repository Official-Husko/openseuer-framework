namespace ObenFramework.Dialogue;

/// <summary>What happened to a write to the Dialogue System's Lua state.</summary>
public enum LuaWriteResult
{
    /// <summary>The value is in the Lua state now.</summary>
    Applied,

    /// <summary>
    /// The game is loading a save (it re-applies the saved Lua state at the very end of a load and would
    /// overwrite the write), or the Lua tables don't exist yet. The write is queued and applied right after
    /// the load finishes, before the framework raises <see cref="Events.GameEvents.Loaded"/>.
    /// </summary>
    Deferred,
}
