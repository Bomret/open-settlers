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
    public void EveryGeneratedObjectIsDrawnInRowMajorOrderWithTheEntryOfItsCode()
    {
        var map = Generated();

        var draws = ObjectDraws.Of(map);

        Assert.IsNotEmpty(map.Objects);
        CollectionAssert.AreEqual(
            map.Objects.Select(o => (o.Y, ObjectSprites.Entry(o.Code)!.Value)).ToList(),
            draws.Select(d => (d.Row, d.Entry)).ToList()
        );
    }

    [TestMethod]
    public void AnObjectIsDrawnSixteenEastAndEightSouthOfItsNodeLoweredByItsHeight()
    {
        var draws = ObjectDraws.Of(Generated());

        // Seed 1234567812345678, size 1. Object (3,0) code 13 on height 5: x = 3 * 32 + 16, y = 0 - 5 * 4 + 8.
        CollectionAssert.Contains(draws, new ObjectDraw(0, 1250, 112, -12));
        // Object (9,0) code 82 on height 29: x = 9 * 32 + 16, y = 0 - 29 * 4 + 8.
        CollectionAssert.Contains(draws, new ObjectDraw(0, 1324, 304, -108));
    }

    [TestMethod]
    public void AResourceIsNeverDrawn()
    {
        var map = Generated();

        Assert.IsNotEmpty(map.Deposits);
        Assert.HasCount(map.Objects.Count, ObjectDraws.Of(map));
    }
}
