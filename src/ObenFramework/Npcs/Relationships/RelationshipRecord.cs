namespace ObenFramework.Npcs;

/// <summary>A copy of one relationship the player has, read from the game's <c>RelationshipController</c>.</summary>
public sealed class RelationshipRecord
{
    /// <summary>The relationship's id: its own id, else its asset path, else its name. What the game's Lua checks use.</summary>
    public string Id { get; }

    /// <summary>The name shown for it.</summary>
    public string Name { get; }

    /// <summary>How the other side feels about the player, -100 to 100.</summary>
    public float Disposition { get; }

    /// <summary>Where the disposition drifts to, a little every day, at the relationship's own speed.</summary>
    public float Target { get; }

    /// <summary>Points per day the disposition moves towards <see cref="Target"/>.</summary>
    public float ChangePerDay { get; }

    /// <summary>The relationship runs: it drifts and sends its gifts and mail. A paused one keeps its values.</summary>
    public bool Active { get; }

    internal RelationshipRecord(RelationshipController.RelationshipInfo info)
    {
        var relationship = info.relationship;
        Id = relationship.Id;
        Name = relationship.relationshipName ?? string.Empty;
        Disposition = info.disposition;
        Target = info.dispositionTarget;
        ChangePerDay = relationship.dispositionChangeSpeed;
        Active = info.active;
    }

    public override string ToString() => $"{Name} ({Id}): {Disposition:0.#} -> {Target:0.#}";
}
