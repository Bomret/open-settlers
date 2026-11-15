namespace OpenSettlers.Import;

/// <summary>The three directions a road strip is drawn from a node (the other three are drawn from the node at the other end).</summary>
public enum RoadDirection
{
    /// <summary>East.</summary>
    East,

    /// <summary>South-east.</summary>
    SouthEast,

    /// <summary>South.</summary>
    South,
}

/// <summary>
/// One strip of road to draw, named in game terms (INV-0010). <c>X</c> and <c>Y</c> are the position in map pixels the
/// source's image offsets hang the image from. <c>Difference</c> is the height of the strip's start node less that of
/// its end node, <c>Slope</c> the slope of the ground across the strip, and <c>Ground</c> the terrain it lies on.
/// </summary>
public readonly record struct RoadStrip
{
    /// <summary>The horizontal position in map pixels the source's image offsets hang the image from.</summary>
    public required int X { get; init; }

    /// <summary>The vertical position in map pixels the source's image offsets hang the image from.</summary>
    public required int Y { get; init; }

    /// <summary>The direction the strip leaves its start node in.</summary>
    public required RoadDirection Direction { get; init; }

    /// <summary>The height of the strip's start node less that of its end node.</summary>
    public required int Difference { get; init; }

    /// <summary>The slope of the ground across the strip.</summary>
    public required int Slope { get; init; }

    /// <summary>The terrain the strip lies on.</summary>
    public required Terrain Ground { get; init; }
}
