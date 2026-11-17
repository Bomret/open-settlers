namespace OpenSettlers.Import;

/// <summary>The kind of a road marker (<c>CONTEXT.md</c>, "Road marker").</summary>
public enum RoadMarkerKind
{
    /// <summary>The end mark, on the end of the road being drawn.</summary>
    End,

    /// <summary>The blocked mark, on a neighbor no step may go to.</summary>
    Blocked,

    /// <summary>The way-back mark, on the node the road came from.</summary>
    WayBack,

    /// <summary>The slope mark, on any other neighbor, for the height difference of the step.</summary>
    Slope,
}

/// <summary>
/// A road marker (<c>CONTEXT.md</c>, "Road marker"): the draw term of the sign the game view shows in road mode on the end
/// of the road being drawn and on each of its six neighbors. It holds the kind and, for a slope, the height difference,
/// no entry number (INV-0010).
/// </summary>
public readonly record struct RoadMarker
{
    /// <summary>The kind of the marker.</summary>
    public required RoadMarkerKind Kind { get; init; }

    /// <summary>For a slope, the neighbor's height less the end's height; 0 for every other kind.</summary>
    public int Difference { get; init; }
}
