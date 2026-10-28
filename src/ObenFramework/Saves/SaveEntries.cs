// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections;
using FullSerializer;

namespace ObenFramework.Saves;

/// <summary>
/// Turns the game's save entries (a GUID plus the object's data) into text and back, with the
/// game's own serializer. A blob is exactly the JSON the game writes for one object in a
/// <c>.tnmt</c> file: <c>{"GUID":"...","Data":{"$type":"...", ...}}</c>.
/// </summary>
internal static class SaveEntries
{
    /// <summary>The entry for <paramref name="guid"/> in <paramref name="entries"/>, or null.</summary>
    public static object Find(IList entries, string guid)
    {
        foreach (var entry in entries)
        {
            if (entry != null && GameSaveLists.GuidOf(entry) == guid)
                return entry;
        }
        return null;
    }

    /// <summary>The entry as game save JSON, or null if the serializer rejected it.</summary>
    public static string Write(object entry)
    {
        try
        {
            return SaveController.Serialize(GameSaveLists.Header, entry);
        }
        catch (Exception e)
        {
            SaveState.Log.Error($"The game could not serialize the state of {GameSaveLists.GuidOf(entry)}", e);
            return null;
        }
    }

    /// <summary>
    /// Reads a blob into a game entry for <paramref name="script"/>, or returns null and says why
    /// in <paramref name="problem"/>. The data's type is forced to the script's own type and the
    /// GUID must match, so a blob can only ever restore the object it was addressed to.
    /// </summary>
    public static object Read(SavableScript script, string guid, string blob, out string problem)
    {
        problem = SaveBlobLimits.RefuseReason(blob);
        if (problem != null)
            return null;

        var entry = fsJsonParser.Parse(blob);
        if (!entry.IsDictionary || !entry.AsDictionary.TryGetValue("Data", out var data) || !data.IsDictionary)
        {
            problem = "the blob is not a save entry (no GUID and Data)";
            return null;
        }
        if (!entry.AsDictionary.TryGetValue("GUID", out var blobGuid) || !blobGuid.IsString || blobGuid.AsString != guid)
        {
            problem = "the blob is for a different GUID than " + guid;
            return null;
        }

        var type = script.GetType();
        data.AsDictionary["$type"] = new fsData(type.FullName);
        problem = SaveBlobTypes.UnsafeReason(entry, type);
        if (problem != null)
            return null;

        var header = SaveController.Deserialize("save state entry", GameSaveLists.Header, fsJsonPrinter.CompressedJson(entry), out var exception);
        if (header == null || exception != null)
        {
            problem = "the game could not read the blob" + (exception != null ? ": " + exception.Message : "");
            return null;
        }
        var restored = GameSaveLists.DataOf(header);
        if (GameSaveLists.GuidOf(header) != guid || restored == null || restored.GetType() != type)
        {
            problem = "the blob did not read back as a " + type.Name;
            return null;
        }
        return header;
    }
}
