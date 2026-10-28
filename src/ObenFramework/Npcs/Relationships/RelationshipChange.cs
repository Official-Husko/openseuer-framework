namespace ObenFramework.Npcs;

/// <summary>What happened to a relationship (see <see cref="Relationships.Changed"/>).</summary>
public enum RelationshipChangeKind
{
    /// <summary>The player gained the relationship.</summary>
    Started,

    /// <summary>The relationship was taken away.</summary>
    Ended,

    /// <summary>The disposition moved: the game's daily drift, or a mod set it.</summary>
    Disposition,
}

/// <summary>One change to a relationship.</summary>
public sealed class RelationshipChange
{
    public RelationshipChangeKind Kind { get; }

    /// <summary>The relationship's id (see <see cref="RelationshipRecord.Id"/>).</summary>
    public string Id { get; }

    public string Name { get; }

    /// <summary>The disposition before the change (for <see cref="RelationshipChangeKind.Started"/>, the starting value).</summary>
    public float Previous { get; }

    /// <summary>The disposition after the change.</summary>
    public float Current { get; }

    internal RelationshipChange(RelationshipChangeKind kind, Relationship relationship, float previous, float current)
    {
        Kind = kind;
        Id = relationship.Id;
        Name = relationship.relationshipName ?? string.Empty;
        Previous = previous;
        Current = current;
    }

    public override string ToString() => $"{Kind} {Name} ({Id}): {Previous:0.#} -> {Current:0.#}";
}
