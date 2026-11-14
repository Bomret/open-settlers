using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// A finished road between two flags (<c>docs/rules/roads.md</c>). The road itself lies in the map: each node of it
/// holds a bit in its flag byte for each of the six directions along which a road leaves it, so a node on the road has
/// two bits and a flag at its end holds one for this road.
/// </summary>
public sealed record Road
{
    /// <summary>The slot (<see cref="FlagTable"/>) of the flag the road starts at.</summary>
    public required int FromSlot { get; init; }

    /// <summary>The direction in which the road leaves the starting flag.</summary>
    public required Direction FromDirection { get; init; }

    /// <summary>The slot of the flag the road ends at.</summary>
    public required int ToSlot { get; init; }

    /// <summary>The direction in which the road leaves the ending flag, back towards the start.</summary>
    public required Direction ToDirection { get; init; }

    /// <summary>The direction of each step from the starting flag, each 0 to 5.</summary>
    public required IReadOnlyList<Direction> Directions { get; init; }

    /// <summary>
    /// True when both triangles beside every step of the road are water; false when each step has land beside it. The
    /// original clears a bit in the flag table's items for a water road (<c>0x444e68</c>); what that bit means is not
    /// recovered.
    /// </summary>
    public required bool Water { get; init; }
}
