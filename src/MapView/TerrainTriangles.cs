using System.Collections.Generic;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

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
                    new TerrainTriangle
                    {
                        Column = column,
                        Row = row,
                        Upper = true,
                        Terrain = tile.Upper,
                        Apex = node,
                        Left = south,
                        Right = southEast,
                        X = x,
                        Y = y - node * HeightStep,
                    }
                );
                (x, y) = Camera.NodePosition(column + 1, row + 1);
                triangles.Add(
                    new TerrainTriangle
                    {
                        Column = column,
                        Row = row,
                        Upper = false,
                        Terrain = tile.Lower,
                        Apex = southEast,
                        Left = node,
                        Right = east,
                        X = x,
                        Y = y - southEast * HeightStep,
                    }
                );
            }
        }

        return triangles;
    }
}
