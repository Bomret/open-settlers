namespace OpenSettlers.Domain;

/// <summary>Where a settler walks along the roads to, or from a castle on its way out: a building, a road, or a storage.</summary>
public abstract record Errand
{
    private protected Errand() { }

    /// <summary>The slot of the flag the errand is bound to.</summary>
    public abstract int Flag { get; }
}

/// <summary>The walk of a worker to the building at a flag, which takes it as its worker.</summary>
public sealed record ToBuilding : Errand
{
    /// <summary>The slot of the building's flag.</summary>
    public required int At { get; init; }

    /// <inheritdoc />
    public override int Flag => At;
}

/// <summary>The walk of a carrier to a road: the road that leaves a flag in a direction.</summary>
public sealed record ToRoad : Errand
{
    /// <summary>The slot of the flag the road leaves.</summary>
    public required int At { get; init; }

    /// <summary>The direction the road leaves the flag in.</summary>
    public required Direction Direction { get; init; }

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
public readonly record struct Load
{
    /// <summary>The good.</summary>
    public required Good Good { get; init; }

    /// <summary>The slot of the flag the good is to go to, or null where it has none.</summary>
    public required int? Destination { get; init; }
}
