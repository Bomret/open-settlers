using System.Collections.Generic;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// One terrain triangle to draw. <c>Column</c> and <c>Row</c> are the tile's, <c>Upper</c> tells which of its two
/// triangles. <c>X</c> and <c>Y</c> are the pixel position of the triangle's anchor node: the tile's node for an
/// upper triangle, the node south-east of it for a lower one, with the node's height already taken off <c>Y</c>.
/// The mask's own offset and its half width are applied where it is drawn.
/// </summary>
public readonly record struct TerrainTriangle(int Column, int Row, bool Upper, int Mask, int Texture, int X, int Y);

/// <summary>The triangles of a map in the order the original paints them.</summary>
public static class TerrainTriangles
{
    /// <summary>Pixels a node rises for each unit of its height.</summary>
    public const int HeightStep = 4;

    /// <summary>
    /// Each tile's upper and then lower triangle, tile by tile along a row and row by row from the north, so that a
    /// later triangle paints over an earlier. A triangle the original has no mask for is left out.
    /// </summary>
    public static List<TerrainTriangle> Of(Map map)
    {
        var width = map.Size.Width;
        var height = map.Size.Height;
        var heights = map.Layer(MapLayer.Height).ToArray();
        var terrain = map.Layer(MapLayer.Terrain).ToArray();
        int H(int column, int row) => heights[row % height * width + column % width] & 31;
        var triangles = new List<TerrainTriangle>(width * height * 2);
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var code = terrain[row * width + column];
                var node = H(column, row);
                var south = H(column, row + 1);
                var southEast = H(column + 1, row + 1);
                var east = H(column + 1, row);
                if (TerrainRules.Upper(node, south, southEast, code >> 4) is { } upper)
                {
                    var (x, y) = Camera.NodePosition(column, row);
                    triangles.Add(
                        new TerrainTriangle(column, row, true, upper.Mask, upper.Texture, x, y - node * HeightStep)
                    );
                }

                if (TerrainRules.Lower(southEast, node, east, code & 15) is { } lower)
                {
                    var (x, y) = Camera.NodePosition(column + 1, row + 1);
                    triangles.Add(
                        new TerrainTriangle(
                            column,
                            row,
                            false,
                            lower.Mask,
                            lower.Texture,
                            x,
                            y - southEast * HeightStep
                        )
                    );
                }
            }
        }

        return triangles;
    }
}
