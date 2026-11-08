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

    /// <summary>Whether no good lies on the place.</summary>
    public bool IsEmpty => Good.IsNone;
}
