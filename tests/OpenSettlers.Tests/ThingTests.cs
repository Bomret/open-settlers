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
            new RuleTables(
                new ObjectClass[RuleTables.ClassCount],
                [.. Enumerable.Range(0, RuleTables.StartStockRows).Select(_ => new byte[StartStock.Goods])]
            )
        );

    [TestMethod]
    public void ANamedMapObjectShowsItsRawCodeThenItsName()
    {
        Assert.AreEqual(
            "object 12,4 code=0x0a tree",
            Thing
                .Of(
                    new PlacedObject
                    {
                        X = 12,
                        Y = 4,
                        Object = new MapObject { Code = 0x0a },
                    },
                    Names()
                )
                .Line()
        );
    }

    [TestMethod]
    public void ACodeWithNoNameShowsOnlyItsRawCode()
    {
        Assert.AreEqual(
            "object 13,4 code=0x31",
            Thing
                .Of(
                    new PlacedObject
                    {
                        X = 13,
                        Y = 4,
                        Object = new MapObject { Code = 0x31 },
                    },
                    Names()
                )
                .Line()
        );
    }

    [TestMethod]
    public void TheFileOrdersThingsByKindThenRowThenColumn()
    {
        var text = Thing.File([
            new Thing
            {
                Kind = "object",
                X = 5,
                Y = 2,
                Values = [("code", "0x31")],
                Name = null,
            },
            new Thing
            {
                Kind = "object",
                X = 9,
                Y = 1,
                Values = [("code", "0x31")],
                Name = null,
            },
            new Thing
            {
                Kind = "object",
                X = 2,
                Y = 2,
                Values = [("code", "0x31")],
                Name = null,
            },
            new Thing
            {
                Kind = "flag",
                X = 7,
                Y = 7,
                Values = [],
                Name = null,
            },
        ]);

        Assert.AreEqual("flag 7,7\nobject 9,1 code=0x31\nobject 2,2 code=0x31\nobject 5,2 code=0x31\n", text);
    }

    [TestMethod]
    public void TheFileOfNoThingsIsEmpty()
    {
        Assert.AreEqual("", Thing.File([]));
    }
}
