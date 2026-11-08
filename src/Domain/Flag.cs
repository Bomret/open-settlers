using System.Linq;

namespace OpenSettlers.Domain;

/// <summary>
/// A flag of the player's: the node it stands on and the item of the original's flag table that goes with it, a side for
/// each of the six directions. The flag's position in the <see cref="FlagTable"/> is its slot in the original's flag table
/// (<c>Table70</c>, <c>docs/rules/flags.md</c>); the same number is the link the node holds in the map's resource byte. A
/// slot whose flag was taken down (<c>docs/rules/removal.md</c>) is free until a new flag takes it, and keeps its item.
/// </summary>
public sealed class Flag
{
    private readonly FlagSide[] _sides = [.. Enumerable.Range(0, Direction.Count).Select(_ => new FlagSide())];

    /// <summary>A flag on the node at (<paramref name="x"/>, <paramref name="y"/>) with an empty item.</summary>
    public Flag(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>The node's column; -1 in a free slot.</summary>
    public int X { get; private set; }

    /// <summary>The node's row; -1 in a free slot.</summary>
    public int Y { get; private set; }

    /// <summary>Whether the slot holds no flag.</summary>
    public bool IsFree => X < 0;

    /// <summary>
    /// Whether a request of this flag for a carrier found none; the retry scan clears it, and while it is set the flag asks
    /// for no further carrier.
    /// </summary>
    public bool CarrierRequestFailed { get; set; }

    /// <summary>The side the road in <paramref name="direction"/> leaves the flag by.</summary>
    public FlagSide Side(Direction direction) => _sides[direction];

    /// <summary>Takes the flag down: the slot has no node and keeps its item.</summary>
    internal void Release()
    {
        X = -1;
        Y = -1;
    }
}
