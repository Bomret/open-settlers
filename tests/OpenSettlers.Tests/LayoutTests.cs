using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class LayoutTests
{
    const string Seed = "1234567812345678";

    // A grid of 32 rows with one row replaced; rows[y] is the replacement text of row y.
    static string Grid(char fill, params (int Row, string Text)[] rows) =>
        "[" + string.Join(", ", Enumerable.Range(0, 32).Select(y => $"\"{rows.FirstOrDefault(r => r.Row == y).Text ?? new string(fill, 32)}\"")) + "]";

    static string Row(char fill, int at, char with) => new string(fill, at) + with + new string(fill, 31 - at);

    static string Text(
        string size = "1",
        string? height = null,
        string? terrain = null,
        string? lower = null,
        string objects = "[]",
        string seed = $"\"{Seed}\"",
        string extra = ""
    ) =>
        $$"""
        // a drawn map
        {
          "size": {{size}},
          "seed": {{seed}},
          "height": {{height ?? Grid('1')}},
          "terrain": {{terrain ?? Grid('g')}},
          {{(lower is null ? "" : $"\"terrainLower\": {lower},")}}
          "objects": {{objects}},{{extra}}
        }
        """;

    static Map Built(string text)
    {
        var read = Layout.Read(text, "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));

        return read.Layout!.BuildMap();
    }

    static int At(int x, int y) => y * 32 + x;

    [TestMethod]
    public void ADrawnMapOfGrassAHillALakeAndTreesLoadsIntoTheLayers()
    {
        var map = Built(
            Text(
                height: Grid('1', (4, Row('1', 5, '9')), (10, Row('1', 3, '0'))),
                terrain: Grid('g', (10, Row('g', 3, 'w'))),
                lower: Grid('g', (10, Row('g', 3, 'x'))),
                objects: """[{"x": 7, "y": 2, "code": 8}, {"x": 8, "y": 2, "code": 9}]"""
            )
        );

        Assert.AreEqual(32, map.Size.Width);
        Assert.AreEqual(9, map.Layer(MapLayer.Height)[At(5, 4)]);
        Assert.AreEqual(0, map.Layer(MapLayer.Height)[At(3, 10)]);
        Assert.AreEqual(0x55, map.Layer(MapLayer.Terrain)[At(0, 0)]);
        Assert.AreEqual(0x01, map.Layer(MapLayer.Terrain)[At(3, 10)]);
        CollectionAssert.AreEqual(new[] { (7, 2, 8), (8, 2, 9) }, map.Objects.Select(o => (o.X, o.Y, (int)o.Code)).ToArray());
    }
}
