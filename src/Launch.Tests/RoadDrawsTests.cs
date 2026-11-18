using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The end-to-end check of the road strips against road-strips.txt (the output of the emulator verb roaddraw:, ADR 0008; the
// file lives in src/Import.HistoryEdition.Tests, which checks the mask and the texture of each strip against it): the map
// view's road pass makes the strip of a node's road, and the History Edition source over the install draws an image for the
// strip where the original has a mask for it and none beyond. Through public types only: the mask and the texture entries are
// the road rules', checked in src/Import.HistoryEdition.Tests.
[TestClass]
public sealed class RoadDrawsTests
{
    const int Column = 10;
    const int Row = 10;

    public static IEnumerable<object[]> Cases =>
        File.ReadAllLines("road-strips.txt").Select(line => new object[] { line });

    [TestMethod]
    [DynamicData(nameof(Cases))]
    [TestCategory(Needs.Install)]
    public void AStripIsDrawnWithAnImageWhereTheOriginalHasAMaskForItAndItsHeightAboveTheNode(string line)
    {
        var source = HistoryEditionSource.Load(TestProgram.OpenInstall(), TestProgram.Tables());
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

        map.At(Column, Row).Roads = RoadSet.Of(direction);
        var mask = int.Parse(fields["mask"], CultureInfo.InvariantCulture);
        var strip = EdgeTerms.Strips(map).Single();

        // The masks of the archive hold a height difference of 4 either way; a strip beyond has none and is not drawn.
        var first = new[] { 230, 239, 248 }[direction];
        if (mask < first || mask > first + 8)
        {
            Assert.IsNull(source.Draw(strip));
            return;
        }

        Assert.IsNotNull(source.Draw(strip),"the source has an image for the strip the original draws");
        // The routine's row is the pass's row less 38 and less four pixels a unit of the node's height, less 2: the
        // strip's top above the node's row of pixels.
        var expectedTop =
            int.Parse(fields["y"], CultureInfo.InvariantCulture)
            - int.Parse(fields["row"], CultureInfo.InvariantCulture)
            + 38;
        Assert.AreEqual(expectedTop, strip.Y - Camera.NodePosition(Column, Row).Y);
    }
}
