using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The lines of road-strips.txt are the output of the emulator verb roaddraw: (tools/GhidraDump/emulate.py), without the
// leading "EMU ". It runs the original's three strip routines (0x428008 east, 0x428105 south-east, 0x4281cc south) on
// random heights and terrain bytes of a node and its north, east, south, south-east, and west neighbours, and reads the
// position, the mask entry, and the texture entry they hand to the textured fill (ADR 0008). The road rules of the
// History Edition source (RoadRules) are checked here against the mask and the texture. The strip's layout and height above
// the node belong to the map view and are checked against the same file in src/MapView.Tests.
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
        var map = new Map(MapSize.Offered[0]);
        var neighbours = new Dictionary<string, (int Dx, int Dy)>
        {
            ["a"] = (0, 0),
            ["n"] = (0, -1),
            ["e"] = (1, 0),
            ["s"] = (0, 1),
            ["se"] = (1, 1),
            ["w"] = (-1, 0),
        };
        foreach (var (name, (dx, dy)) in neighbours)
        {
            var values = fields[name].Split(',').Select(int.Parse).ToArray();
            ref var node = ref map.At(Column + dx, Row + dy);
            node.Height = values[0];
            // The fixture holds the original's terrain byte: the upper triangle in the high nibble.
            node.Upper = new Terrain { Code = values[1] >> 4 };
            node.Lower = new Terrain { Code = values[1] & 0xf };
        }

        var mask = int.Parse(fields["mask"], CultureInfo.InvariantCulture);
        var draw = RoadRules.Of(StripOf(map, (RoadDirection)direction));

        // The masks of the archive hold a height difference of 4 either way; a strip beyond has none and is not drawn.
        var first = new[] { 230, 239, 248 }[direction];
        if (mask < first || mask > first + 8)
        {
            Assert.IsNull(draw);
            return;
        }

        Assert.AreEqual(mask, draw!.Value.Mask);
        Assert.AreEqual(int.Parse(fields["texture"], CultureInfo.InvariantCulture), draw.Value.Texture);
    }

    [TestMethod]
    public void AWaterRoadTakesTheWaterTextureAndMountainAndSnowStepTheTexture()
    {
        // A level strip (difference 0, slope 0) over water, over mountain, and over snow.
        Assert.AreEqual(309, RoadRules.Of(Level(0))!.Value.Texture);
        Assert.AreEqual(304, RoadRules.Of(Level(9))!.Value.Texture);
        Assert.AreEqual(307, RoadRules.Of(Level(15))!.Value.Texture);
    }

    static RoadStrip Level(int ground) =>
        new()
        {
            X = 0,
            Y = 0,
            Direction = RoadDirection.SouthEast,
            Difference = 0,
            Slope = 0,
            Ground = new Terrain { Code = ground },
        };

    // The strip of the road that leaves the node (Column, Row) in a direction: the height difference to the node it joins, and
    // the slope sum and the ground of the edge (docs/rules/roads.md). The position is none: the picks do not read it. The map
    // view builds the same values (src/MapView.Tests checks them against the same file); the History Edition source cannot
    // reach it, so the test builds them here from the domain's map.
    static RoadStrip StripOf(Map map, RoadDirection direction)
    {
        int H(int dx, int dy) => map.At(Column + dx, Row + dy).Height;
        Terrain Low(int dx, int dy) => map.At(Column + dx, Row + dy).Lower;
        Terrain High(int dx, int dy) => map.At(Column + dx, Row + dy).Upper;

        var here = H(0, 0);
        var (difference, slope, ground) = direction switch
        {
            RoadDirection.East => (
                here - H(1, 0),
                -3 * (here - H(1, 0)) + H(0, -1) - H(1, 1),
                Terrain.Higher(Low(0, 0), High(0, -1))
            ),
            RoadDirection.SouthEast => (here - H(1, 1), 2 * (H(1, 0) - H(0, 1)), Terrain.Higher(Low(0, 0), High(0, 0))),
            _ => (here - H(0, 1), 3 * (here - H(0, 1)) - H(-1, 0) + H(1, 1), Terrain.Higher(High(0, 0), Low(-1, 0))),
        };

        return new RoadStrip
        {
            X = 0,
            Y = 0,
            Direction = direction,
            Difference = difference,
            Slope = slope,
            Ground = ground,
        };
    }
}
