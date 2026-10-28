// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Saves;
using ObenLoader;

namespace CheatMenu.Dev;

/// <summary>Kept next to a save as mods/CheatMenu.dev.json once the developer menu changed it.</summary>
internal sealed class DevMarkData
{
    public bool Modified;
}

/// <summary>
/// Remembers in the save that the developer menu changed its state (a quest rewound, a flag set), so a
/// bug report from that save can say so. Only a change that went through marks it.
/// </summary>
internal static class DevMarks
{
    private static ModSaveData<DevMarkData> _data;
    private static ObenMod _mod;
    private static bool _warned;

    public static bool Modified => _data != null && _data.Value.Modified;

    public static void Init(ObenMod mod)
    {
        _mod = mod;
        _warned = false;
        _data = ModSaves.Register<DevMarkData>(mod, "dev");
    }

    public static void Mark()
    {
        if (_data == null || _data.Value.Modified)
            return;
        _data.Value.Modified = true;
        if (_warned)
            return;
        _warned = true;
        _mod.Log.Warn("The developer menu changed this save; it is marked as dev-modified (mods/CheatMenu.dev.json).");
    }
}
