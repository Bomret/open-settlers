using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The strips of the road pass on public map view types. The tests that read road-strips.txt and the road rules of the
// History Edition import (internal to it) are in tests/OpenSettlers.Tests.
[TestClass]
public sealed class RoadDrawsTests
{
    [TestMethod]
    public void TheStripsLieBetweenTheNodesTheyJoin()
    {
        var map = Blank();
        map.At(5, 5).Roads = RoadSet.Of(0);
        map.At(8, 6).Roads = RoadSet.Of(1);
        map.At(12, 7).Roads = RoadSet.Of(2);
        Land(map);

        var strips = EdgeTerms.Strips(map);

        // On flat land the strips are the level masks, each beside the node's pixel position, in the order of the road
        // pass's walk from west to east.
        Assert.AreEqual(3, strips.Count);
        var land = Terrain.Lowland;
        Assert.AreEqual(
            new RoadStrip
            {
                X = Camera.NodePosition(5, 5).X,
                Y = Camera.NodePosition(5, 5).Y - 2,
                Direction = RoadDirection.East,
                Difference = 0,
                Slope = 0,
                Ground = land,
            },
            strips[0]
        );
        Assert.AreEqual(
            new RoadStrip
            {
                X = Camera.NodePosition(8, 6).X,
                Y = Camera.NodePosition(8, 6).Y - 2,
                Direction = RoadDirection.SouthEast,
                Difference = 0,
                Slope = 0,
                Ground = land,
            },
            strips[1]
        );
        Assert.AreEqual(
            new RoadStrip
            {
                X = Camera.NodePosition(12, 7).X - 16,
                Y = Camera.NodePosition(12, 7).Y - 2,
                Direction = RoadDirection.South,
                Difference = 0,
                Slope = 0,
                Ground = land,
            },
            strips[2]
        );
    }

    [TestMethod]
    public void ARoadAcrossTheWrappingEdgeIsDrawnFromTheNodeBeforeItWithTheHeightOfTheNodeAfter()
    {
        var map = Blank();
        var last = map.Size.Width - 1;
        map.At(last, 3).Roads = RoadSet.Of(0);
        map.At(last, 3).Height = 5;
        map.At(0, 3).Height = 3;
        Land(map);

        var strip = EdgeTerms.Strips(map).Single();

        // The strip of the east road leaves the last column and its mask is that of the difference 5 - 3 = 2, which
        // the node at column 0 gives: the heights wrap with the map.
        Assert.AreEqual(2, strip.Difference);
        Assert.AreEqual(Camera.NodePosition(last, 3).X, strip.X);
        Assert.AreEqual(Camera.NodePosition(last, 3).Y - 5 * TerrainTriangles.HeightStep - 2, strip.Y);
    }

    [TestMethod]
    public void TheDirectionsWestNorthWestAndNorthAreDrawnFromTheNodeAtTheOtherEnd()
    {
        var map = Blank();
        map.At(5, 5).Roads = RoadSet.Of(3).With(4).With(5);

        Assert.IsEmpty(EdgeTerms.Strips(map));
    }

    static Map Blank() => new(MapSize.Offered[0]);

    // Every triangle of the map is the lowest land.
    static void Land(Map map)
    {
        for (var index = 0; index < map.Count; index++)
        {
            map.At(index).Upper = Terrain.Lowland;
            map.At(index).Lower = Terrain.Lowland;
        }
    }
}
