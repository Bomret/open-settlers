using System;
using System.Collections.Generic;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

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
public readonly record struct RoadStrip(
    int X,
    int Y,
    RoadDirection Direction,
    int Difference,
    int Slope,
    Terrain Ground
);

/// <summary>
/// Where the roads of a map are drawn and over what (<c>docs/rules/roads.md</c>). The original draws
/// a road a step at a time, as a strip of the road texture over the terrain, from each node that holds the bit of
/// the direction east, south-east, or south (<c>0x427d3e</c>, <c>0x427e3a</c>, <c>0x427f1f</c> with <c>0x428008</c>,
/// <c>0x428105</c>, and <c>0x4281cc</c>); the other three directions are drawn from the node at the other end.
/// </summary>
public static class RoadDraws
{
    // Pixels the strip reaches above its higher node (0x428008 subtracts 2 more after the height); the level strip is
    // 5 rows high, so it is centered on the line.
    private const int Above = 2;

    /// <summary>
    /// The strips of the map's roads in row-major order of the node each is drawn from. Every strip gives a term; the
    /// source decides which it draws.
    /// </summary>
    public static List<RoadStrip> Of(Map map)
    {
        var strips = new List<RoadStrip>();
        var width = map.Size.Width;
        for (var row = 0; row < map.Size.Height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var roads = map.At(column, row).Roads;
                for (var direction = 0; direction < 3; direction++)
                {
                    if (roads.Has(direction))
                    {
                        strips.Add(Strip(map, column, row, (RoadDirection)direction));
                    }
                }
            }
        }

        return strips;
    }

    // The strip of the road that leaves a node east, south-east, or south.
    private static RoadStrip Strip(Map map, int column, int row, RoadDirection direction)
    {
        int H(int dx, int dy) => map.At(column + dx, row + dy).Height;
        Terrain Low(int dx, int dy) => map.At(column + dx, row + dy).Lower;
        Terrain High(int dx, int dy) => map.At(column + dx, row + dy).Upper;

        var (x, y) = Camera.NodePosition(column, row);
        var here = H(0, 0);
        int difference,
            slope,
            top,
            left;
        Terrain ground;
        switch (direction)
        {
            case RoadDirection.East:
                // East: the node and its east neighbour, the strip as high as the higher of them.
                difference = here - H(1, 0);
                slope = -3 * difference + H(0, -1) - H(1, 1);
                ground = Terrain.Higher(Low(0, 0), High(0, -1));
                top = y - Math.Max(here, H(1, 0)) * TerrainTriangles.HeightStep;
                left = x;
                break;
            case RoadDirection.SouthEast:
                // South-east: the strip starts at the node's own height.
                difference = here - H(1, 1);
                slope = 2 * (H(1, 0) - H(0, 1));
                ground = Terrain.Higher(Low(0, 0), High(0, 0));
                top = y - here * TerrainTriangles.HeightStep;
                left = x;
                break;
            default:
                // South: the strip lies west of the node.
                difference = here - H(0, 1);
                slope = 3 * difference - H(-1, 0) + H(1, 1);
                ground = Terrain.Higher(High(0, 0), Low(-1, 0));
                top = y - here * TerrainTriangles.HeightStep;
                left = x - Camera.NodeSpacing / 2;
                break;
        }

        return new RoadStrip(left, top - Above, direction, difference, slope, ground);
    }
}
