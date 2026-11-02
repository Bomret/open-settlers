using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ObjectDrawsTests
{
    static Map Generated() => MapViewState.Open("1234567812345678", 1).Map!;

    [TestMethod]
    public void EveryGeneratedObjectIsDrawnOnItsTileInRowMajorOrder()
    {
        var map = Generated();

        var draws = ObjectDraws.Of(map);

        Assert.IsNotEmpty(map.Objects);
        CollectionAssert.AreEqual(
            map.Objects.Select(o => (o.X, o.Y, (int)o.Code)).ToList(),
            draws.Select(d => (d.Column, d.Row, d.Code)).ToList()
        );
    }

    [TestMethod]
    public void AnObjectSitsSixteenPixelsEastAndEightSouthOfItsNodeLoweredByItsHeight()
    {
        var map = Generated();
        var heights = map.Layer(MapLayer.Height);

        foreach (var draw in ObjectDraws.Of(map))
        {
            var (x, y) = Camera.NodePosition(draw.Column, draw.Row);
            var height = heights[draw.Row * map.Size.Width + draw.Column] & 31;
            Assert.AreEqual(x + 16, draw.X);
            Assert.AreEqual(y - height * TerrainTriangles.HeightStep + 8, draw.Y);
        }
    }

    [TestMethod]
    public void TheDrawsCarryTheSpriteEntryOfTheirCode()
    {
        foreach (var draw in ObjectDraws.Of(Generated()))
        {
            Assert.AreEqual(ObjectSprites.Entry(draw.Code), draw.Entry);
        }
    }

    [TestMethod]
    public void AResourceIsNeverDrawn()
    {
        var map = Generated();
        var objectTiles = map.Objects.Select(o => (o.X, o.Y)).ToHashSet();

        Assert.IsNotEmpty(map.Deposits);
        Assert.IsTrue(ObjectDraws.Of(map).All(d => objectTiles.Contains((d.Column, d.Row))));
        Assert.HasCount(map.Objects.Count, ObjectDraws.Of(map));
    }
}
