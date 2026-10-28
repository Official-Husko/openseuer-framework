namespace ObenFramework.Npcs;

/// <summary>A mod NPC's routine moved to another step (see <see cref="NpcRoutines.StepChanged"/>).</summary>
public sealed class NpcStepChange
{
    /// <summary>Id of the mod the NPC belongs to.</summary>
    public string Owner { get; }

    /// <summary>The NPC's name within its mod.</summary>
    public string Name { get; }

    public NpcStepKind From { get; }

    public NpcStepKind To { get; }

    /// <summary>The scene of the new step, or null when the NPC is now away.</summary>
    public string Scene { get; }

    /// <summary>The NPC's character id while it is spawned in the scene you are in, else 0.</summary>
    public int CharacterId { get; }

    internal NpcStepChange(string owner, string name, NpcStepKind from, NpcStepKind to, string scene, int characterId)
    {
        Owner = owner;
        Name = name;
        From = from;
        To = to;
        Scene = scene;
        CharacterId = characterId;
    }

    public override string ToString() => $"{Owner}/{Name}: {From} -> {To}{(Scene != null ? " in " + Scene : "")}";
}
