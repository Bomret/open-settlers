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
    public void AResourceIsNeverDrawn()
    {
        var map = Generated();

        Assert.IsNotEmpty(map.Deposits);
        Assert.HasCount(map.Objects.Count, ObjectDraws.Of(map));
    }
}
