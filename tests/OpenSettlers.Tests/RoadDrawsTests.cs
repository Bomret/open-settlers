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
    const int Column = 10;
    const int Row = 10;

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
        var draw = RoadRules.Of(strip);

        // The masks of the archive hold a height difference of 4 either way; a strip beyond has none and is not drawn.
        var first = new[] { 230, 239, 248 }[direction];
        if (mask < first || mask > first + 8)
        {
            Assert.IsNull(draw);
            return;
        }

        Assert.AreEqual(mask, draw!.Value.Mask);
        Assert.AreEqual(int.Parse(fields["texture"], CultureInfo.InvariantCulture), draw.Value.Texture);
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
    public void AWaterRoadTakesTheWaterTextureAndMountainAndSnowStepTheTexture()
    {
        var map = Blank();
        map.At(5, 5).Roads = RoadSet.Of(1);
        SetTerrain(map, 5, 5, 0);
        Assert.AreEqual(309, RoadRules.Of(EdgeTerms.Strips(map).Single())!.Value.Texture);

        SetTerrain(map, 5, 5, 9);
        Assert.AreEqual(304, RoadRules.Of(EdgeTerms.Strips(map).Single())!.Value.Texture);

        SetTerrain(map, 5, 5, 15);
        Assert.AreEqual(307, RoadRules.Of(EdgeTerms.Strips(map).Single())!.Value.Texture);
    }

    static Map Blank() => new(MapSize.Offered[0]);

    // Both triangles of a node take the terrain code.
    static void SetTerrain(Map map, int x, int y, int code)
    {
        map.At(x, y).Upper = new Terrain { Code = code };
        map.At(x, y).Lower = new Terrain { Code = code };
    }
}
