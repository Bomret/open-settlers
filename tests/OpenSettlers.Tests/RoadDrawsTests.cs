using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of road-strips.txt are the output of the emulator verb roaddraw: (tools/GhidraDump/emulate.py), without the
// leading "EMU ". It runs the original's three strip routines (0x428008 east, 0x428105 south-east, 0x4281cc south) on
// random heights and terrain bytes of a node and its north, east, south, south-east, and west neighbours, and reads the
// position, the mask entry, and the texture entry they hand to the textured fill (ADR 0008). The position's row is the
// row the pass hands in less the 38 pixels the routine takes off and less the strip's height above the node line; the
// engine's own layout is checked against it.
[TestClass]
public sealed class RoadDrawsTests
{
    private const int Column = 10;
    private const int Row = 10;

    public static IEnumerable<object[]> Cases =>
        File.ReadAllLines("road-strips.txt").Select(line => new object[] { line });

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void AStripHasTheMaskAndTheTextureOfTheOriginalsRoutineAndItsHeightAboveTheNode(string line)
    {
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
            var node = map.Index(Column + dx, Row + dy);
            map.Bytes(MapLayer.Height)[node] = (byte)values[0];
            map.Bytes(MapLayer.Terrain)[node] = (byte)values[1];
        }

        map.Bytes(MapLayer.Flags)[map.Index(Column, Row)] = (byte)(1 << direction);
        var mask = int.Parse(fields["mask"], CultureInfo.InvariantCulture);
        var target = new[] { "e", "se", "s" }[direction];
        var strips = RoadDraws.Of(map);

        // The masks of the archive hold a height difference of 4 either way; a strip beyond has none and is not drawn.
        var first = new[] { 230, 239, 248 }[direction];
        if (mask < first || mask > first + 8)
        {
            Assert.IsEmpty(strips);
            return;
        }

        var strip = strips.Single();
        Assert.AreEqual(mask, strip.Mask);
        Assert.AreEqual(int.Parse(fields["texture"], CultureInfo.InvariantCulture), strip.Texture);
        // The routine's row is the pass's row less 38 and less four pixels a unit of the node's height (the higher node's
        // for the east strip), less 2: the strip's top above the node's row of pixels.
        var position = Camera.NodePosition(Column, Row);
        var expectedTop =
            int.Parse(fields["y"], CultureInfo.InvariantCulture)
            - int.Parse(fields["row"], CultureInfo.InvariantCulture)
            + 38;
        Assert.AreEqual(expectedTop, strip.Y - position.Y);
        Assert.AreEqual(heights["a"] - heights[target], mask - first - 4);
    }

    [TestMethod]
    public void TheStripsLieBetweenTheNodesTheyJoin()
    {
        var map = Blank();
        var flags = map.Bytes(MapLayer.Flags);
        flags[map.Index(5, 5)] = 0x01;
        flags[map.Index(8, 6)] = 0x02;
        flags[map.Index(12, 7)] = 0x04;
        var land = map.Bytes(MapLayer.Terrain);
        Array.Fill(land, (byte)0x55);

        var strips = RoadDraws.Of(map);

        // On flat land the strips are the level masks, each beside the node's pixel position, and rows are in order.
        Assert.AreEqual(3, strips.Count);
        Assert.AreEqual(
            new RoadStrip(Camera.NodePosition(5, 5).X, Camera.NodePosition(5, 5).Y - 2, 234, 301),
            strips[0]
        );
        Assert.AreEqual(
            new RoadStrip(Camera.NodePosition(8, 6).X, Camera.NodePosition(8, 6).Y - 2, 243, 301),
            strips[1]
        );
        Assert.AreEqual(
            new RoadStrip(Camera.NodePosition(12, 7).X - 16, Camera.NodePosition(12, 7).Y - 2, 252, 301),
            strips[2]
        );
    }

    [TestMethod]
    public void ARoadAcrossTheWrappingEdgeIsDrawnFromTheNodeBeforeItWithTheHeightOfTheNodeAfter()
    {
        var map = Blank();
        var last = map.Size.Width - 1;
        map.Bytes(MapLayer.Flags)[map.Index(last, 3)] = 0x01;
        map.Bytes(MapLayer.Height)[map.Index(last, 3)] = 5;
        map.Bytes(MapLayer.Height)[map.Index(0, 3)] = 3;
        Array.Fill(map.Bytes(MapLayer.Terrain), (byte)0x55);

        var strip = RoadDraws.Of(map).Single();

        // The strip of the east road leaves the last column and its mask is that of the difference 5 - 3 = 2, which
        // the node at column 0 gives: the heights wrap with the map.
        Assert.AreEqual(236, strip.Mask);
        Assert.AreEqual(Camera.NodePosition(last, 3).X, strip.X);
        Assert.AreEqual(Camera.NodePosition(last, 3).Y - 5 * TerrainTriangles.HeightStep - 2, strip.Y);
    }

    [TestMethod]
    public void AWaterRoadTakesTheWaterTextureAndMountainAndSnowStepTheTexture()
    {
        var map = Blank();
        map.Bytes(MapLayer.Flags)[map.Index(5, 5)] = 0x02;
        map.Bytes(MapLayer.Terrain)[map.Index(5, 5)] = 0x00;
        Assert.AreEqual(309, RoadDraws.Of(map).Single().Texture);

        map.Bytes(MapLayer.Terrain)[map.Index(5, 5)] = 0x99;
        Assert.AreEqual(304, RoadDraws.Of(map).Single().Texture);

        map.Bytes(MapLayer.Terrain)[map.Index(5, 5)] = 0xff;
        Assert.AreEqual(307, RoadDraws.Of(map).Single().Texture);
    }

    [TestMethod]
    public void TheDirectionsWestNorthWestAndNorthAreDrawnFromTheNodeAtTheOtherEnd()
    {
        var map = Blank();
        map.Bytes(MapLayer.Flags)[map.Index(5, 5)] = 0x38;

        Assert.IsEmpty(RoadDraws.Of(map));
    }

    private static Map Blank() => new(MapSize.Offered[0]);
}
