// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Events;

/// <summary>
/// Notices a level change or save load the game announced but never carried out: the save was missing
/// or broken, a migration failed, or a level change stayed in the same scene (a door to another spot
/// of the level only saves and moves the player). The game then simply fades its loading screen out
/// again, without any event. A load counts as dropped once, after the frame it was announced in, the
/// game's loading flag is off and its loading screen is idle while no new scene started loading. No
/// Unity types, so it is tested on its own.
/// </summary>
internal sealed class LoadAbortRules
{
    private int _announcedFrame;

    /// <summary>A load or level change was announced and hasn't started or been dropped yet.</summary>
    public bool Pending { get; private set; }

    /// <summary>The pending announcement included a level change (doors, fast travel).</summary>
    public bool LevelChange { get; private set; }

    /// <summary>The world was ready when the first announcement came.</summary>
    public bool WasReady { get; private set; }

    /// <summary>The game announced a level change (<paramref name="levelChange"/>) or a save load.</summary>
    public void Announced(int frame, bool levelChange, bool worldReady)
    {
        if (!Pending)
        {
            Pending = true;
            LevelChange = false;
            WasReady = worldReady;
        }
        _announcedFrame = frame;
        LevelChange |= levelChange;
    }

    /// <summary>A scene started loading or the game began restoring a save: the load is really happening.</summary>
    public void Proceeding()
    {
        Pending = false;
        LevelChange = false;
    }

    /// <summary>
    /// Checked every frame while <see cref="Pending"/>. True once when the announced load was dropped;
    /// <see cref="LevelChange"/> and <see cref="WasReady"/> still describe it until the next announcement.
    /// </summary>
    public bool Check(int frame, bool gameLoading, bool screenIdle)
    {
        if (!Pending || frame <= _announcedFrame || gameLoading || !screenIdle)
            return false;
        Pending = false;
        return true;
    }

    public void Reset()
    {
        Pending = false;
        LevelChange = false;
        WasReady = false;
    }
}
