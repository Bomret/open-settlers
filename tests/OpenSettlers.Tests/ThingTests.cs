using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ThingTests
{
    static NameTable Names() =>
        new(
            new ProgramTables(
                new byte[ProgramTables.ClassCount],
                Enumerable.Range(0, ProgramTables.StartStockRows).Select(_ => new byte[StartStock.Goods]).ToArray()
            )
        );

    [TestMethod]
    public void ANamedMapObjectShowsItsRawCodeThenItsName()
    {
        Assert.AreEqual(
            "object 12,4 code=0x0a tree",
            Thing.Of(new PlacedObject(12, 4, new MapObject(0x0a)), Names()).Line()
        );
    }

    [TestMethod]
    public void ACodeWithNoNameShowsOnlyItsRawCode()
    {
        Assert.AreEqual(
            "object 13,4 code=0x31",
            Thing.Of(new PlacedObject(13, 4, new MapObject(0x31)), Names()).Line()
        );
    }

    [TestMethod]
    public void TheFileOrdersThingsByKindThenRowThenColumn()
    {
        var text = Thing.File([
            new Thing("object", 5, 2, [("code", "0x31")], null),
            new Thing("object", 9, 1, [("code", "0x31")], null),
            new Thing("object", 2, 2, [("code", "0x31")], null),
            new Thing("flag", 7, 7, [], null),
        ]);

        Assert.AreEqual("flag 7,7\nobject 9,1 code=0x31\nobject 2,2 code=0x31\nobject 5,2 code=0x31\n", text);
    }

    [TestMethod]
    public void TheFileOfNoThingsIsEmpty()
    {
        Assert.AreEqual("", Thing.File([]));
    }
}
