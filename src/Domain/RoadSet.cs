using System.Numerics;

namespace OpenSettlers.Domain;

/// <summary>
/// The directions in which a road leaves a node: a subset of the six directions 0 east, 1 south-east, 2 south, 3 west,
/// 4 north-west, 5 north (<c>docs/rules/roads.md</c>). A flag's building is joined to it as a road in direction 4, and
/// a building's node holds the road in direction 1 to its flag.
/// </summary>
/// <param name="Bits">The original's six flag bits, bit <c>d</c> for direction <c>d</c>; kept raw for the exporters.</param>
public readonly record struct RoadSet(byte Bits)
{
    /// <summary>No road leaves the node.</summary>
    public static RoadSet Empty => default;

    /// <summary>True when no road leaves the node.</summary>
    public bool IsEmpty => Bits == 0;

    /// <summary>The number of directions in the set.</summary>
    public int Count => BitOperations.PopCount(Bits);

    /// <summary>The lowest direction in the set; 32 for the empty set.</summary>
    public int First => BitOperations.TrailingZeroCount(Bits);

    /// <summary>The highest direction in the set; -1 for the empty set.</summary>
    public int Last => 31 - BitOperations.LeadingZeroCount((uint)Bits);

    /// <summary>The set of one direction.</summary>
    public static RoadSet Of(int direction) => new((byte)(1 << direction));

    /// <summary>True when a road leaves in the direction.</summary>
    public bool Has(int direction) => (Bits >> direction & 1) != 0;

    /// <summary>True when a road leaves in exactly this one direction.</summary>
    public bool IsOnly(int direction) => Bits == 1 << direction;

    /// <summary>The set with the direction added.</summary>
    public RoadSet With(int direction) => new((byte)(Bits | 1 << direction));

    /// <summary>The set with the direction removed.</summary>
    public RoadSet Without(int direction) => new((byte)(Bits & ~(1 << direction)));

    /// <summary>The directions both sets hold.</summary>
    public RoadSet Both(RoadSet other) => new((byte)(Bits & other.Bits));

    /// <summary>The directions of either set.</summary>
    public RoadSet Either(RoadSet other) => new((byte)(Bits | other.Bits));

    /// <summary>The directions this set holds that the other does not.</summary>
    public RoadSet Except(RoadSet other) => new((byte)(Bits & ~other.Bits));
}
