using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Where the border posts of a map stand (<c>docs/rules/roads.md</c>, "How a border is drawn"). The original draws a post
/// on the edge east, south-east, or south of a node where the node holds no road bit of that direction and the owner
/// values of the two nodes differ (<c>0x427d3e</c>, <c>0x427e3a</c>, <c>0x427f1f</c> with <c>0x428389</c>,
/// <c>0x42846d</c>, and <c>0x428528</c>). A node nobody owns counts as an owner value of its own.
/// </summary>
public static class BorderDraws
{
    // The anchor lies one pixel left of the middle of the edge and 4 pixels above it.
    const int Left = 1;
    const int Above = 4;

    /// <summary>
    /// The post on the edge that leaves a node in a direction, or null where the node holds the road bit of that
    /// direction or the two owners are equal.
    /// </summary>
    internal static BorderPost? Post(Map map, int column, int row, RoadDirection direction)
    {
        var node = map.At(column, row);
        var (dx, dy) = direction switch
        {
            RoadDirection.East => (1, 0),
            RoadDirection.SouthEast => (1, 1),
            _ => (0, 1),
        };
        var other = map.At(column + dx, row + dy);
        if (node.Roads.Has((int)direction) || node.Owner == other.Owner)
        {
            return null;
        }

        var (x, y) = Camera.NodePosition(column, row);
        var (otherX, otherY) = Camera.NodePosition(column + dx, row + dy);
        var (slope, ground) = RoadDraws.SlopeAndGround(map, column, row, direction);

        return new BorderPost
        {
            Column = column,
            Row = row,
            X = (x + otherX) / 2 - Left,
            Y = (y + otherY) / 2 - Above - (node.Height + other.Height) * TerrainTriangles.HeightStep / 2,
            Direction = direction,
            Slope = slope,
            Ground = ground,
        };
    }
}
