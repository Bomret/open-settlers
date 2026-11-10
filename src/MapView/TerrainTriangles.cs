using System.Collections.Generic;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// One terrain triangle to draw, named in game terms (INV-0010). <c>Column</c> and <c>Row</c> are the tile's,
/// <c>Upper</c> tells which of its two triangles, <c>Terrain</c> is the triangle's terrain. <c>Apex</c>, <c>Left</c>,
/// and <c>Right</c> are the heights of its three nodes: the apex is the tile's node for an upper triangle, the node
/// south-east of it for a lower one; left and right are the nodes south and south-east of the tile's node for an upper
/// triangle, and the tile's node and the node east of it for a lower one. <c>X</c> and <c>Y</c> are the pixel position
/// of the apex, with its height already taken off <c>Y</c>. The source's image offsets hang the image from it.
/// </summary>
public readonly record struct TerrainTriangle(
    int Column,
    int Row,
    bool Upper,
    Terrain Terrain,
    int Apex,
    int Left,
    int Right,
    int X,
    int Y
);

/// <summary>The triangles of a map in the order the original paints them.</summary>
public static class TerrainTriangles
{
    /// <summary>Pixels a node rises for each unit of its height.</summary>
    public const int HeightStep = 4;

    /// <summary>
    /// Each tile's upper and then lower triangle, tile by tile along a row and row by row from the north, so that a
    /// later triangle paints over an earlier. Every triangle gives a term; the source decides which it draws.
    /// </summary>
    public static List<TerrainTriangle> Of(Map map)
    {
        var width = map.Size.Width;
        var height = map.Size.Height;
        int H(int column, int row) => map.At(column, row).Height;
        var triangles = new List<TerrainTriangle>(width * height * 2);
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var tile = map.At(column, row);
                var node = H(column, row);
                var south = H(column, row + 1);
                var southEast = H(column + 1, row + 1);
                var east = H(column + 1, row);
                var (x, y) = Camera.NodePosition(column, row);
                triangles.Add(
                    new TerrainTriangle(column, row, true, tile.Upper, node, south, southEast, x, y - node * HeightStep)
                );
                (x, y) = Camera.NodePosition(column + 1, row + 1);
                triangles.Add(
                    new TerrainTriangle(
                        column,
                        row,
                        false,
                        tile.Lower,
                        southEast,
                        node,
                        east,
                        x,
                        y - southEast * HeightStep
                    )
                );
            }
        }

        return triangles;
    }
}
