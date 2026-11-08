namespace OpenSettlers.Domain;

/// <summary>Where a settler walks along the roads to, or from a castle on its way out: a building, a road, or a storage.</summary>
public abstract record Errand
{
    private protected Errand() { }

    /// <summary>The slot of the flag the errand is bound to.</summary>
    public abstract int Flag { get; }
}

/// <summary>The walk of a worker to the building at a flag, which takes it as its worker.</summary>
/// <param name="At">The slot of the building's flag.</param>
public sealed record ToBuilding(int At) : Errand
{
    /// <inheritdoc />
    public override int Flag => At;
}

/// <summary>The walk of a carrier to a road: the road that leaves a flag in a direction.</summary>
/// <param name="At">The slot of the flag the road leaves.</param>
/// <param name="Direction">The direction the road leaves the flag in.</param>
public sealed record ToRoad(int At, Direction Direction) : Errand
{
    /// <inheritdoc />
    public override int Flag => At;
}

/// <summary>
/// The walk home along the roads to a storage. The castle is the only storage, and the walk picks it at its first flag: the
/// errand's flag is the castle's, slot 0.
/// </summary>
public sealed record ToStorage : Errand
{
    /// <inheritdoc />
    public override int Flag => 0;
}

/// <summary>A good a carrier carries, and the flag it goes to.</summary>
/// <param name="Good">The good.</param>
/// <param name="Destination">The slot of the flag the good is to go to, or null where it has none.</param>
public readonly record struct Load(Good Good, int? Destination);
