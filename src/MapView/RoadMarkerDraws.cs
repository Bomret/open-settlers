using System.Collections.Generic;
using OpenSettlers.Domain;
using OpenSettlers.Import;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The road markers of a road being drawn (<c>docs/rules/roads.md</c>, "The road markers"): the end mark on the road's end
/// and, on each of its six neighbors, the marker of the road cursor's verdict, each at its node raised by the node's height
/// times <see cref="TerrainTriangles.HeightStep"/>. Reading them changes nothing in the game.
/// </summary>
public static class RoadMarkerDraws
{
    /// <summary>The markers of <paramref name="drawing"/>'s road; none where no road is being drawn.</summary>
    public static List<RoadMarkerDraw> Of(Game game, RoadDrawing drawing)
    {
        if (!drawing.Active)
        {
            return [];
        }

        var nodes = drawing.Nodes;

        return Of(game.Map, nodes, game.RoadPlanner.Verdicts(nodes[0].X, nodes[0].Y, drawing.Directions));
    }

    /// <summary>
    /// The end mark on the last of <paramref name="nodes"/>, the road's nodes unwrapped, and on the neighbor in each
    /// direction 0 to 5 the marker of its verdict: the blocked mark, the way-back mark, or the slope mark of the neighbor's
    /// height less the end's. The end mark comes first, then the neighbors in the order of the directions.
    /// </summary>
    public static List<RoadMarkerDraw> Of(
        Map map,
        IReadOnlyList<(int X, int Y)> nodes,
        IReadOnlyList<RoadCursorVerdict> verdicts
    )
    {
        var (endX, endY) = nodes[^1];
        var endHeight = map.At(endX, endY).Height;
        var draws = new List<RoadMarkerDraw> { Draw(map, endX, endY, RoadMarker.Of(RoadMarkerKind.End)) };
        for (var direction = 0; direction < 6; direction++)
        {
            var (dx, dy) = RoadBuilding.Steps[direction];
            var (column, row) = (endX + dx, endY + dy);
            var marker = verdicts[direction] switch
            {
                RoadCursorVerdict.Blocked => RoadMarker.Of(RoadMarkerKind.Blocked),
                RoadCursorVerdict.WayBack => RoadMarker.Of(RoadMarkerKind.WayBack),
                _ => RoadMarker.Slope(map.At(column, row).Height - endHeight),
            };
            draws.Add(Draw(map, column, row, marker));
        }

        return draws;
    }

    static RoadMarkerDraw Draw(Map map, int column, int row, RoadMarker marker)
    {
        var (x, y) = Camera.NodePosition(column, row);

        return new RoadMarkerDraw
        {
            Row = row,
            X = x,
            Y = y - map.At(column, row).Height * TerrainTriangles.HeightStep,
            Column = column,
            Marker = marker,
        };
    }
}
