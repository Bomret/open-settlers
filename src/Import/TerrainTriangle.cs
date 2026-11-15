namespace OpenSettlers.Import;

/// <summary>
/// One terrain triangle to draw, named in game terms (INV-0010). <c>Column</c> and <c>Row</c> are the tile's,
/// <c>Upper</c> tells which of its two triangles, <c>Terrain</c> is the triangle's terrain. <c>Apex</c>, <c>Left</c>,
/// and <c>Right</c> are the heights of its three nodes: the apex is the tile's node for an upper triangle, the node
/// south-east of it for a lower one; left and right are the nodes south and south-east of the tile's node for an upper
/// triangle, and the tile's node and the node east of it for a lower one. <c>X</c> and <c>Y</c> are the pixel position
/// of the apex, with its height already taken off <c>Y</c>. The source's image offsets hang the image from it.
/// </summary>
public readonly record struct TerrainTriangle
{
    /// <summary>The column of the tile.</summary>
    public required int Column { get; init; }

    /// <summary>The row of the tile.</summary>
    public required int Row { get; init; }

    /// <summary>Whether the triangle is the tile's upper one.</summary>
    public required bool Upper { get; init; }

    /// <summary>The triangle's terrain.</summary>
    public required Terrain Terrain { get; init; }

    /// <summary>The height of the apex node.</summary>
    public required int Apex { get; init; }

    /// <summary>The height of the left node.</summary>
    public required int Left { get; init; }

    /// <summary>The height of the right node.</summary>
    public required int Right { get; init; }

    /// <summary>The horizontal pixel position of the apex.</summary>
    public required int X { get; init; }

    /// <summary>The vertical pixel position of the apex, with its height already taken off.</summary>
    public required int Y { get; init; }
}
