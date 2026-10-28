// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Saves;
using ObenLoader;

namespace ObenFramework.Quests;

/// <summary>
/// Where the definitions of mod tasks are kept: <c>&lt;save&gt;/mods/ObenFramework.quests.json</c>, written
/// with every game save and read back with it. The task itself (its state, texts and counters) is in the
/// game's own save, because a mod's task is a plain game task object that the game saves like any other.
/// If a mod is removed, its tasks stay in the log as they were and this file keeps a few unused lines;
/// neither breaks the save.
/// </summary>
internal static class QuestRegistry
{
    private static ModSaveData<QuestBook> _data;

    public static void Init(ObenMod framework) => _data = ModSaves.Register<QuestBook>(framework, "quests");

    /// <summary>The definitions of the loaded save. Empty in a new game and at the main menu.</summary>
    public static QuestBook Book
    {
        get
        {
            var book = _data?.Value;
            if (book != null)
                book.Quests ??= new System.Collections.Generic.List<QuestRecord>();
            return book ?? new QuestBook();
        }
    }

    public static QuestRecord Find(string taskId) => Book.Find(taskId);

    public static QuestObjectiveRecord FindObjective(string taskId, string key) => QuestBook.FindObjective(Find(taskId), key);
}
