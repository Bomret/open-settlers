namespace OpenSettlers.Import;

/// <summary>
/// One border post to draw, named in game terms (INV-0010): the post on the edge that leaves the node at
/// <c>Column</c> and <c>Row</c> in <c>Direction</c>, where the owners of the two nodes differ and no road lies
/// (<c>docs/rules/roads.md</c>, "How a border is drawn"). <c>X</c> and <c>Y</c> are the position in map pixels the
/// source's image offsets hang the image from, <c>Slope</c> the slope of the ground across the edge, and <c>Ground</c>
/// the terrain it stands on.
/// </summary>
public readonly record struct BorderPost
{
    /// <summary>The column of the node the edge leaves.</summary>
    public required int Column { get; init; }

    /// <summary>The row of the node the edge leaves.</summary>
    public required int Row { get; init; }

    /// <summary>The horizontal position in map pixels the source's image offsets hang the image from.</summary>
    public required int X { get; init; }

    /// <summary>The vertical position in map pixels the source's image offsets hang the image from.</summary>
    public required int Y { get; init; }

    /// <summary>The direction the edge leaves its node in.</summary>
    public required RoadDirection Direction { get; init; }

    /// <summary>The slope of the ground across the edge.</summary>
    public required int Slope { get; init; }

    /// <summary>The terrain the post stands on.</summary>
    public required Terrain Ground { get; init; }
}
