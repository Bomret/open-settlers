using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Import;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The road markers the game view draws while a road is drawn (docs/rules/roads.md, "The road markers"): the end mark on the
// road's end and one marker on each of its six neighbors, at the neighbor's place and height, from the road cursor's
// verdicts.
[TestClass]
public sealed class RoadMarkerDrawsTests
{
    static (int X, int Y) Position(Map map, int column, int row)
    {
        var (x, y) = Camera.NodePosition(column, row);

        return (x, y - map.At(column, row).Height * TerrainTriangles.HeightStep);
    }

    static void AssertMarkers(Game game, RoadDrawing drawing, params RoadMarkerKind[] expected)
    {
        var map = game.Map;
        var end = drawing.Nodes[^1];
        var draws = RoadMarkerDraws.Of(game, drawing);

        Assert.HasCount(7, draws);
        Assert.AreEqual(new RoadMarker { Kind = RoadMarkerKind.End }, draws[0].Marker);
        Assert.AreEqual(Position(map, end.X, end.Y), (draws[0].X, draws[0].Y));
        for (var direction = 0; direction < 6; direction++)
        {
            var (dx, dy) = RoadBuilding.Steps[direction];
            var (column, row) = (end.X + dx, end.Y + dy);
            var draw = draws[direction + 1];
            var difference =
                expected[direction] == RoadMarkerKind.Slope
                    ? map.At(column, row).Height - map.At(end.X, end.Y).Height
                    : 0;

            Assert.AreEqual(
                new RoadMarker { Kind = expected[direction], Difference = difference },
                draw.Marker,
                $"direction {direction}"
            );
            Assert.AreEqual(Position(map, column, row), (draw.X, draw.Y), $"direction {direction}");
            Assert.AreEqual((column, row), (draw.Column, draw.Row), $"direction {direction}");
        }
    }

    static RoadMarkerKind[] KindsOf(IReadOnlyList<RoadCursorVerdict> verdicts) =>
        [
            .. verdicts.Select(verdict =>
                verdict switch
                {
                    RoadCursorVerdict.Blocked => RoadMarkerKind.Blocked,
                    RoadCursorVerdict.WayBack => RoadMarkerKind.WayBack,
                    _ => RoadMarkerKind.Slope,
                }
            ),
        ];

    [TestMethod]
    public void NoRoadBeingDrawnDrawsNoMarker()
    {
        var game = RoadsTests.TwoFlags();

        Assert.IsEmpty(RoadMarkerDraws.Of(game, new RoadDrawing(game)));
    }

    [TestMethod]
    public void AtTheStartFlagTheEndMarkAndEachNeighborsMarkerShowFromTheCursorsVerdicts()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);
        drawing.Click(16, 6);

        var verdicts = game.RoadPlanner.Verdicts(16, 6, []);

        Assert.IsFalse(verdicts.Contains(RoadCursorVerdict.WayBack));
        Assert.IsTrue(verdicts.Contains(RoadCursorVerdict.Open));
        AssertMarkers(game, drawing, KindsOf(verdicts));
    }

    [TestMethod]
    public void AfterAStepTheMarkersStandAroundTheNewEndWithTheWayBackAndAfterAStepBackAroundTheStart()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);
        drawing.Click(16, 6);
        drawing.Click(17, 6);

        var verdicts = game.RoadPlanner.Verdicts(16, 6, [0]);
        Assert.AreEqual(RoadCursorVerdict.WayBack, verdicts[3]);
        AssertMarkers(game, drawing, KindsOf(verdicts));

        drawing.Click(16, 6);
        AssertMarkers(game, drawing, KindsOf(game.RoadPlanner.Verdicts(16, 6, [])));
    }

    // A road across the map's edge keeps its nodes unwrapped, so its markers stand beside its end past the edge.
    [TestMethod]
    public void ARoadAcrossTheMapsEdgeKeepsItsMarkersBesideItsEnd()
    {
        var map = new Map(MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            map.At(index).Height = (byte)(index % 5);
        }

        var width = map.Size.Width;
        (int X, int Y)[] nodes = [(width - 1, 4), (width, 4)];
        RoadCursorVerdict[] verdicts =
        [
            RoadCursorVerdict.Open,
            RoadCursorVerdict.Open,
            RoadCursorVerdict.Blocked,
            RoadCursorVerdict.WayBack,
            RoadCursorVerdict.Open,
            RoadCursorVerdict.Blocked,
        ];

        var draws = RoadMarkerDraws.Of(map, nodes, verdicts);

        Assert.AreEqual(Position(map, width, 4), (draws[0].X, draws[0].Y));
        Assert.AreEqual(Position(map, width + 1, 4), (draws[1].X, draws[1].Y));
        Assert.AreEqual(
            new RoadMarker { Kind = RoadMarkerKind.Slope, Difference = map.At(1, 4).Height - map.At(0, 4).Height },
            draws[1].Marker
        );
        Assert.AreEqual(new RoadMarker { Kind = RoadMarkerKind.WayBack }, draws[4].Marker);
        Assert.AreEqual(Position(map, width - 1, 4), (draws[4].X, draws[4].Y));
    }

    // A marker stands on its neighbor's drawn point, so a click on it picks that node, and the road steps as a click on
    // the node does.
    [TestMethod]
    public void AClickOnANeighborsMarkerPicksItsNodeAndStepsTheRoad()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);
        drawing.Click(16, 6);
        var draws = RoadMarkerDraws.Of(game, drawing);

        foreach (var draw in draws.Skip(1))
        {
            var picked = MapPicking.Pick(game.Map, draw.X, draw.Y, 1, 640, 480, 320, 240);

            Assert.AreEqual((draw.Column & (game.Map.Size.Width - 1), draw.Row & (game.Map.Size.Height - 1)), picked);
        }

        var east = draws[1];
        Assert.AreEqual(RoadMarkerKind.Slope, east.Marker.Kind);
        var (column, row) = MapPicking.Pick(game.Map, east.X, east.Y, 1, 640, 480, 320, 240);
        drawing.Click(column, row);
        Assert.AreEqual((17, 6), drawing.Nodes[^1]);
    }

    // A change of the game to the nodes around the end while the road is drawn changes the markers the next time they are
    // asked for: a neighbor that passes to another owner shows the blocked mark.
    [TestMethod]
    public void TheMarkersFollowAChangeToTheNodesAroundTheEnd()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);
        drawing.Click(16, 6);
        drawing.Click(17, 6);
        var before = RoadMarkerDraws.Of(game, drawing).Select(draw => draw.Marker).ToArray();

        game.Map.At(18, 7).Owner = Owner.Player(1);

        var after = RoadMarkerDraws.Of(game, drawing);
        Assert.AreNotEqual(RoadMarkerKind.Blocked, before[2].Kind);
        Assert.AreEqual(new RoadMarker { Kind = RoadMarkerKind.Blocked }, after[2].Marker);
    }
}
