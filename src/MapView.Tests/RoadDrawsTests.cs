using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The strips of the road pass on public map view types. The road rules of the History Edition import (internal to it) are
// checked against the mask and texture of road-strips.txt in src/Import.HistoryEdition.Tests, and the strip with the
// install's images in src/Launch.Tests.
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

    // The lines of road-strips.txt (linked from src/Import.HistoryEdition.Tests, which checks the mask and the texture of each
    // strip against the road rules) hold the position, the mask entry, and the texture entry the original's routines hand to the
    // fill, for the heights and terrain bytes of a node and its neighbours. The strip's height difference and its height above
    // the node are the map view's, and the mask the original picks carries the difference: mask = first + 4 + difference.
    public static IEnumerable<object[]> Cases =>
        File.ReadAllLines("road-strips.txt").Select(line => new object[] { line });

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void AStripHasTheMaskAndTheTextureOfTheOriginalsRoutineAndItsHeightAboveTheNode(string line)
    {
        const int Column = 10;
        const int Row = 10;
        var fields = line.Split(' ')
            .Select(word => word.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0], pair => pair[1]);
        var direction = int.Parse(fields["dir"], CultureInfo.InvariantCulture);
        var map = Blank();
        var neighbours = new Dictionary<string, (int Dx, int Dy)>
        {
            ["a"] = (0, 0),
            ["n"] = (0, -1),
            ["e"] = (1, 0),
            ["s"] = (0, 1),
            ["se"] = (1, 1),
            ["w"] = (-1, 0),
        };
        var heights = new Dictionary<string, int>();
        foreach (var (name, (dx, dy)) in neighbours)
        {
            var values = fields[name].Split(',').Select(int.Parse).ToArray();
            heights[name] = values[0];
            ref var node = ref map.At(Column + dx, Row + dy);
            node.Height = values[0];
            // The fixture holds the original's terrain byte: the upper triangle in the high nibble.
            node.Upper = new Terrain { Code = values[1] >> 4 };
            node.Lower = new Terrain { Code = values[1] & 0xf };
        }

        map.At(Column, Row).Roads = RoadSet.Of(direction);
        var mask = int.Parse(fields["mask"], CultureInfo.InvariantCulture);
        var target = new[] { "e", "se", "s" }[direction];
        // The builder gives a term for every strip, also one the archive has no mask for.
        var strip = EdgeTerms.Strips(map).Single();

        // The masks of the archive hold a height difference of 4 either way; a strip beyond has none and is not drawn.
        var first = new[] { 230, 239, 248 }[direction];
        if (mask < first || mask > first + 8)
        {
            Assert.IsGreaterThan(4, System.Math.Abs(strip.Difference));
            return;
        }

        Assert.AreEqual(heights["a"] - heights[target], mask - first - 4);
        Assert.AreEqual(mask - first - 4, strip.Difference);
        Assert.AreEqual((RoadDirection)direction, strip.Direction);
        // The routine's row is the pass's row less 38 and less four pixels a unit of the node's height (the higher node's
        // for the east strip), less 2: the strip's top above the node's row of pixels.
        var position = Camera.NodePosition(Column, Row);
        var expectedTop =
            int.Parse(fields["y"], CultureInfo.InvariantCulture)
            - int.Parse(fields["row"], CultureInfo.InvariantCulture)
            + 38;
        Assert.AreEqual(expectedTop, strip.Y - position.Y);
    }

    // The ground of the strip over water, over mountain, and over snow: the road rules step the texture by it (the History
    // Edition source's tests).
    [TestMethod]
    public void AWaterRoadTakesTheWaterTextureAndMountainAndSnowStepTheTexture()
    {
        var map = Blank();
        map.At(5, 5).Roads = RoadSet.Of(1);
        SetTerrain(map, 5, 5, 0);
        Assert.IsTrue(EdgeTerms.Strips(map).Single().Ground.IsWater);

        SetTerrain(map, 5, 5, 9);
        var mountain = EdgeTerms.Strips(map).Single().Ground;
        Assert.IsTrue(mountain.IsAbovePlain && !mountain.IsSnow);

        SetTerrain(map, 5, 5, 15);
        Assert.IsTrue(EdgeTerms.Strips(map).Single().Ground.IsSnow);
    }

    // Both triangles of a node take the terrain code.
    static void SetTerrain(Map map, int x, int y, int code)
    {
        map.At(x, y).Upper = new Terrain { Code = code };
        map.At(x, y).Lower = new Terrain { Code = code };
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
