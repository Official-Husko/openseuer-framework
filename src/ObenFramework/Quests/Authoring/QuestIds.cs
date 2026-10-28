// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Quests;

/// <summary>
/// Ids and limits of the tasks mods make. A mod's task id is its mod id, a dot and a key of its own,
/// so ownership can be read back from the id after a restart or a load. The game's own tasks have no dot
/// in their id and always come from an asset path, so they can never look like a mod's.
/// No Unity types, so it is unit tested on its own.
/// </summary>
internal static class QuestIds
{
    public const int MaxModId = 64;
    public const int MaxKey = 48;
    public const int MaxTitle = 100;
    public const int MaxDescription = 1000;
    public const int MaxObjectives = 12;
    public const int MaxTarget = 9999;
    public const int MaxCheckpointName = 32;

    /// <summary>What a hidden objective shows in the task panel until it is revealed.</summary>
    public const string Placeholder = "???";

    /// <summary>1 to 64 letters, digits, '_', '-' or '.', not starting or ending with a dot.</summary>
    public static bool ValidModId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxModId || id[0] == '.' || id[id.Length - 1] == '.')
            return false;
        foreach (char c in id)
        {
            if (!IsKeyChar(c) && c != '.')
                return false;
        }
        return true;
    }

    /// <summary>1 to 48 letters, digits, '_' or '-'. No dot, so the owner can be split off a task id.</summary>
    public static bool ValidKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxKey)
            return false;
        foreach (char c in key)
        {
            if (!IsKeyChar(c))
                return false;
        }
        return true;
    }

    /// <summary>The task id for a mod's key, or null when either is not allowed.</summary>
    public static string TaskId(string modId, string key) =>
        ValidModId(modId) && ValidKey(key) ? modId + "." + key : null;

    /// <summary>The mod id a mod task id starts with, or null for an id no mod could have made.</summary>
    public static string OwnerOf(string taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return null;
        int dot = taskId.LastIndexOf('.');
        if (dot <= 0 || dot >= taskId.Length - 1)
            return null;
        string modId = taskId.Substring(0, dot);
        return ValidModId(modId) && ValidKey(taskId.Substring(dot + 1)) ? modId : null;
    }

    /// <summary>The key part of a mod task id, or null.</summary>
    public static string KeyOf(string taskId) =>
        OwnerOf(taskId) == null ? null : taskId.Substring(taskId.LastIndexOf('.') + 1);

    public static bool Owns(string modId, string taskId) =>
        modId != null && string.Equals(OwnerOf(taskId), modId, System.StringComparison.Ordinal);

    /// <summary>
    /// A task a mod made: a mod-style id and no asset path (the game's tasks all have one). Same answer as
    /// <see cref="OwnerOf"/> != null, without allocating, since the game compares tasks in loops.
    /// </summary>
    public static bool IsModTask(string id, string assetPath)
    {
        if (!string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(id))
            return false;
        int dot = id.LastIndexOf('.');
        if (dot <= 0 || dot >= id.Length - 1 || dot > MaxModId || id.Length - dot - 1 > MaxKey)
            return false;
        if (id[0] == '.' || id[dot - 1] == '.')
            return false;
        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];
            if (i > dot ? !IsKeyChar(c) : !IsKeyChar(c) && c != '.')
                return false;
        }
        return true;
    }

    /// <summary>
    /// Text the task panel can show: at most <paramref name="max"/> characters, no control characters
    /// (newlines only when <paramref name="multiline"/>), and not blank when <paramref name="required"/>.
    /// </summary>
    public static bool ValidText(string text, int max, bool required, bool multiline)
    {
        if (text == null)
            return !required;
        if (text.Length > max || (required && text.Trim().Length == 0))
            return false;
        foreach (char c in text)
        {
            if (char.IsControl(c) && !(multiline && (c == '\n' || c == '\r')))
                return false;
        }
        return true;
    }

    /// <summary>No item (0 and 0), or an item id with 1 to <see cref="MaxTarget"/> of it.</summary>
    public static bool ValidItem(int itemId, int count)
    {
        if (itemId < 0 || count < 0 || count > MaxTarget)
            return false;
        return itemId == 0 ? count == 0 : count > 0;
    }

    /// <summary>1 to 32 letters, digits or '-'.</summary>
    public static bool ValidCheckpointName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxCheckpointName)
            return false;
        foreach (char c in name)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-';
            if (!ok)
                return false;
        }
        return true;
    }

    /// <summary>How the game names an objective in its Lua lists ("taskId_objectiveId").</summary>
    public static string ObjectiveEntry(string taskId, string objectiveKey) => taskId + "_" + objectiveKey;

    private static bool IsKeyChar(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
}
