using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The castle's goods (docs/rules/castle.md, docs/rules/construction-materials.md): the stock is keyed by the entries of the content
// catalog, with a place for every good of it that starts at 0 (ADR 0020).
[TestClass]
public sealed class CastleStockTests
{
    static readonly ContentCatalog Catalog = TestCatalog.WithAiToken();

    static readonly Castle Where = new()
    {
        X = 1,
        Y = 1,
        Height = 1,
    };

    [TestMethod]
    public void TheFoodCountsAreTheSixFoodGoodsInTheOrderOfTheFoodReadout()
    {
        var castle = Stocked((1, 11), (2, 12), (3, 13), (4, 14), (5, 15), (6, 16), (7, 17));

        CollectionAssert.AreEqual(new[] { 11, 12, 13, 14, 15, 16 }, castle.FoodCounts.ToArray());
        CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0, 0 }, new CastleStock(Catalog).FoodCounts.ToArray());
        Assert.AreEqual(14, castle.Count(Good.Grain));
    }

    static CastleStock Stocked(params (int Good, int Count)[] counts)
    {
        var castle = new CastleStock(Catalog);
        castle.Place(Where, counts.ToDictionary(entry => TestCatalog.GoodOf(entry.Good), entry => entry.Count));

        return castle;
    }

    [TestMethod]
    public void ALogIsCountedByItsName()
    {
        var castle = Stocked();

        castle.Add(Good.Log);

        Assert.AreEqual(1, castle.Count(Good.Log));
        Assert.AreEqual(1, castle.Logs);
        Assert.AreEqual(0, castle.Planks);
    }

    [TestMethod]
    public void PlanksAndStonesAreCountedByTheirNames()
    {
        var castle = Stocked((8, 53), (10, 28));

        Assert.AreEqual(53, castle.Planks);
        Assert.AreEqual(28, castle.Stones);
        Assert.AreEqual(53, castle.Count(new Good { Name = "plank" }));
    }

    [TestMethod]
    public void TakingAGoodLowersItsCountAndTakesNothingFromAnEmptyEntry()
    {
        var castle = Stocked((8, 1));

        Assert.IsTrue(castle.Take(Good.Plank));
        Assert.AreEqual(0, castle.Planks);
        Assert.IsFalse(castle.Take(Good.Plank), "no plank is left to take");
        Assert.AreEqual(0, castle.Planks);
    }

    [TestMethod]
    public void AnAdditionAtTheCapLeavesTheCountAt50000()
    {
        var castle = Stocked((8, 50000));

        castle.Add(Good.Plank);

        Assert.AreEqual(50000, castle.Planks);
    }

    [TestMethod]
    public void AdditionsFrom49999PastTheCapStopAt50000()
    {
        var castle = Stocked((8, 49999));

        castle.Add(Good.Plank);
        Assert.AreEqual(50000, castle.Planks);
        castle.Add(Good.Plank);
        Assert.AreEqual(50000, castle.Planks);
    }

    [TestMethod]
    public void EveryGoodOfTheCatalogHasAPlaceThatStartsAtZero()
    {
        var castle = Stocked();

        Assert.AreEqual(Catalog.Goods.Length, castle.Stock.Count);
        foreach (var entry in Catalog.Goods)
        {
            Assert.AreEqual(0, castle.Count(new Good { Name = entry.Name }), entry.Name);
        }
    }

    [TestMethod]
    public void AGoodWithNoOriginalCodeIsCountedAddedTakenAndCappedByItsName()
    {
        var castle = Stocked();

        castle.Add(TestCatalog.AiToken);
        castle.Add(TestCatalog.AiToken);

        Assert.AreEqual(2, castle.Count(TestCatalog.AiToken));
        Assert.IsTrue(castle.Take(TestCatalog.AiToken));
        Assert.AreEqual(1, castle.Count(TestCatalog.AiToken));
        Assert.AreEqual(0, castle.Count(Good.Log), "no other good moved");

        var full = new CastleStock(Catalog);
        full.Place(Where, new Dictionary<Good, int> { [TestCatalog.AiToken] = 50000 });
        full.Add(TestCatalog.AiToken);
        Assert.AreEqual(50000, full.Count(TestCatalog.AiToken));
    }

    [TestMethod]
    public void AGoodTheCatalogLacksStopsEveryOperationNamingIt()
    {
        var castle = Stocked();
        var lacking = new Good { Name = "unobtainium" };

        foreach (
            var action in new Action[]
            {
                () => castle.Count(lacking),
                () => castle.Add(lacking),
                () => castle.Take(lacking),
            }
        )
        {
            var error = Assert.ThrowsExactly<InvalidOperationException>(action);
            StringAssert.Contains(error.Message, "unobtainium");
        }
    }

    [TestMethod]
    public void AStartStockThatNamesAGoodTheCatalogLacksStopsTheCastle()
    {
        var castle = new CastleStock(Catalog);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            castle.Place(Where, new Dictionary<Good, int> { [new Good { Name = "unobtainium" }] = 1 })
        );

        StringAssert.Contains(error.Message, "unobtainium");
    }

    [TestMethod]
    public void NoGoodHasNoCountAndAddsNothing()
    {
        var castle = Stocked((1, 5));

        castle.Add(Good.None);

        Assert.AreEqual(0, castle.Count(Good.None));
        Assert.IsFalse(castle.Take(Good.None));
    }
}
