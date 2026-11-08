using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// A finished road between two flags (<c>docs/rules/roads.md</c>). The road itself lies in the map: each node of it
/// holds a bit in its flag byte for each of the six directions along which a road leaves it, so a node on the road has
/// two bits and a flag at its end holds one for this road.
/// </summary>
/// <param name="FromSlot">The slot (<see cref="FlagTable"/>) of the flag the road starts at.</param>
/// <param name="FromDirection">The direction in which the road leaves the starting flag.</param>
/// <param name="ToSlot">The slot of the flag the road ends at.</param>
/// <param name="ToDirection">The direction in which the road leaves the ending flag, back towards the start.</param>
/// <param name="Directions">The direction of each step from the starting flag, each 0 to 5.</param>
/// <param name="Water">
/// True when both triangles beside every step of the road are water; false when each step has land beside it. The
/// original clears a bit in the flag table's items for a water road (<c>0x444e68</c>); what that bit means is not
/// recovered.
/// </param>
public sealed record Road(
    int FromSlot,
    int FromDirection,
    int ToSlot,
    int ToDirection,
    IReadOnlyList<int> Directions,
    bool Water
);
