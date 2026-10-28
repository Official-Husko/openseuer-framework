// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace ObenFramework.Saves;

/// <summary>
/// Access to the parts of <c>SaveController</c> that are not public: the nested entry type
/// <c>ObjectDataHeader</c> (a GUID and the live object) and the two in-memory lists a save or load
/// works on. Looked up once; if a game update renames any of them <see cref="Ready"/> is false and
/// capture and apply refuse instead of failing in odd ways.
/// </summary>
internal static class GameSaveLists
{
    public static readonly Type Header;
    public static readonly FieldInfo HeaderGuid;
    public static readonly FieldInfo HeaderData;
    private static readonly FieldInfo GlobalField;
    private static readonly FieldInfo LevelField;

    static GameSaveLists()
    {
        try
        {
            Header = typeof(SaveController).GetNestedType("ObjectDataHeader", BindingFlags.NonPublic | BindingFlags.Public);
            HeaderGuid = Header?.GetField("GUID");
            HeaderData = Header?.GetField("Data");
            GlobalField = AccessTools.Field(typeof(SaveController), "tempSavedata_Global");
            LevelField = AccessTools.Field(typeof(SaveController), "tempSavedata_Level");
        }
        catch (Exception)
        {
            // Left null; Ready reports it.
        }
    }

    public static bool Ready =>
        Header != null && HeaderGuid != null && HeaderData != null && GlobalField != null && LevelField != null;

    /// <summary>A new empty list of the game's entry type.</summary>
    public static IList NewList() => (IList)Activator.CreateInstance(GlobalField.FieldType);

    public static string GuidOf(object entry) => (string)HeaderGuid.GetValue(entry);

    public static object DataOf(object entry) => HeaderData.GetValue(entry);

    /// <summary>
    /// Points the game's global and level lists at the given ones until disposed, then puts the
    /// originals back. The game rebuilds its GUID lookup when the list instance changes, so the
    /// swap leaves nothing stale behind.
    /// </summary>
    public readonly struct Swap : IDisposable
    {
        private readonly object _oldGlobal;
        private readonly object _oldLevel;

        public Swap(IList global, IList level)
        {
            _oldGlobal = GlobalField.GetValue(null);
            _oldLevel = LevelField.GetValue(null);
            GlobalField.SetValue(null, global);
            LevelField.SetValue(null, level);
        }

        public void Dispose()
        {
            GlobalField.SetValue(null, _oldGlobal);
            LevelField.SetValue(null, _oldLevel);
        }
    }
}
