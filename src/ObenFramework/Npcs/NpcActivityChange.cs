namespace ObenFramework.Npcs;

/// <summary>An NPC moved from one activity to another (see <see cref="Npcs.ActivityChanged"/>).</summary>
public sealed class NpcActivityChange
{
    /// <summary>The NPC's character id.</summary>
    public int NpcId { get; }

    /// <summary>Name of the activity before, or null when it had none.</summary>
    public string From { get; }

    /// <summary>Name of the activity now, or null when it has none.</summary>
    public string To { get; }

    internal NpcActivityChange(int npcId, string from, string to)
    {
        NpcId = npcId;
        From = from;
        To = to;
    }

    public override string ToString() => $"NPC {NpcId}: {From ?? "nothing"} -> {To ?? "nothing"}";
}
