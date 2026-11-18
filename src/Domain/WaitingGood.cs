namespace OpenSettlers.Domain;

/// <summary>
/// One of the eight places of a flag, where a good lies that waits for a carrier (<c>docs/rules/goods.md</c>): the good,
/// the flag it is to go to, and the direction of the road it was given. A place with no good keeps the destination of the
/// last good it held, as the original's word does.
/// </summary>
public sealed class WaitingGood
{
    /// <summary>The good on the place; <see cref="Good.None"/> where the place is empty.</summary>
    public Good Good { get; internal set; }

    /// <summary>The slot of the flag the good is to go to, or null where it has none yet.</summary>
    public int? Destination { get; internal set; }

    /// <summary>The direction of the first road toward the destination, or null where the good has been given none.</summary>
    public Direction? Way { get; internal set; }

    /// <summary>A good is put on the place with a destination and a direction (none for either where it has none).</summary>
    public void Lay(Good good, int? destination, Direction? way)
    {
        Good = good;
        Destination = destination;
        Way = way;
    }

    /// <summary>A good takes the place of the one on it: it has no direction yet and keeps its destination.</summary>
    public void Replace(Good good, int? destination)
    {
        Good = good;
        Way = null;
        Destination = destination;
    }

    /// <summary>The good leaves the place: it is empty and its direction is gone, and it keeps the destination.</summary>
    public void Clear()
    {
        Good = Good.None;
        Way = null;
    }

    /// <summary>The good is to go to a flag, or to none.</summary>
    public void SendTo(int? destination) => Destination = destination;

    /// <summary>The good is given the direction of the first road toward its destination, or none.</summary>
    public void Direct(Direction? way) => Way = way;

    /// <summary>Whether no good lies on the place.</summary>
    public bool IsEmpty => Good.IsNone;
}
