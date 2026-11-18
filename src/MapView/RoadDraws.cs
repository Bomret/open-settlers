using System;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Where the roads of a map are drawn and over what (<c>docs/rules/roads.md</c>). The original draws
/// a road a step at a time, as a strip of the road texture over the terrain, from each node that holds the bit of
/// the direction east, south-east, or south (<c>0x427d3e</c>, <c>0x427e3a</c>, <c>0x427f1f</c> with <c>0x428008</c>,
/// <c>0x428105</c>, and <c>0x4281cc</c>); the other three directions are drawn from the node at the other end.
/// </summary>
static class RoadDraws
{
    // Pixels the strip reaches above its higher node (0x428008 subtracts 2 more after the height); the level strip is
    // 5 rows high, so it is centered on the line.
    const int Above = 2;

    // The strip of the road that leaves a node east, south-east, or south. Every strip gives a term; the source decides
    // which it draws.
    internal static RoadStrip Strip(Map map, int column, int row, RoadDirection direction)
    {
        int H(int dx, int dy) => map.At(column + dx, row + dy).Height;

        var (x, y) = Camera.NodePosition(column, row);
        var here = H(0, 0);
        var (slope, ground) = SlopeAndGround(map, column, row, direction);
        int difference,
            top,
            left;
        switch (direction)
        {
            case RoadDirection.East:
                // East: the node and its east neighbour, the strip as high as the higher of them.
                difference = here - H(1, 0);
                top = y - Math.Max(here, H(1, 0)) * TerrainTriangles.HeightStep;
                left = x;
                break;
            case RoadDirection.SouthEast:
                // South-east: the strip starts at the node's own height.
                difference = here - H(1, 1);
                top = y - here * TerrainTriangles.HeightStep;
                left = x;
                break;
            default:
                // South: the strip lies west of the node.
                difference = here - H(0, 1);
                top = y - here * TerrainTriangles.HeightStep;
                left = x - Camera.NodeSpacing / 2;
                break;
        }

        return new RoadStrip
        {
            X = left,
            Y = top - Above,
            Direction = direction,
            Difference = difference,
            Slope = slope,
            Ground = ground,
        };
    }

    /// <summary>
    /// The slope sum and the ground of the edge that leaves a node east, south-east, or south: the road strip's and the
    /// border post's alike (<c>docs/rules/roads.md</c>).
    /// </summary>
    internal static (int Slope, Terrain Ground) SlopeAndGround(Map map, int column, int row, RoadDirection direction)
    {
        int H(int dx, int dy) => map.At(column + dx, row + dy).Height;
        Terrain Low(int dx, int dy) => map.At(column + dx, row + dy).Lower;
        Terrain High(int dx, int dy) => map.At(column + dx, row + dy).Upper;

        var here = H(0, 0);

        return direction switch
        {
            RoadDirection.East => (-3 * (here - H(1, 0)) + H(0, -1) - H(1, 1), Terrain.Higher(Low(0, 0), High(0, -1))),
            RoadDirection.SouthEast => (2 * (H(1, 0) - H(0, 1)), Terrain.Higher(Low(0, 0), High(0, 0))),
            _ => (3 * (here - H(0, 1)) - H(-1, 0) + H(1, 1), Terrain.Higher(High(0, 0), Low(-1, 0))),
        };
    }
}
