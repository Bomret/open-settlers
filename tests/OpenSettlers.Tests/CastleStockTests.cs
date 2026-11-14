using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The castle's goods (docs/rules/castle.md, docs/rules/construction-materials.md): the list holds the 26 counts of the storage item
// from its word at 6, so the count of the good g is the entry g - 1 (the storage item's word for a log, the good 7, lies at 0x12).
[TestClass]
public sealed class CastleStockTests
{
    static CastleStock Stocked(params (int Good, int Count)[] counts)
    {
        var stock = new int[StartStock.Goods];
        foreach (var (good, count) in counts)
        {
            stock[good - 1] = count;
        }

        var castle = new CastleStock();
        castle.Place(new Castle(1, 1, 1), stock);

        return castle;
    }

    [TestMethod]
    public void ALogIsCountedInTheEntryOfTheGoodSeven()
    {
        var castle = Stocked();

        castle.Add(Good.Log);

        Assert.AreEqual(1, castle.Stock[6]);
        Assert.AreEqual(1, castle.Logs);
        Assert.AreEqual(0, castle.Planks);
    }

    [TestMethod]
    public void PlanksAndStonesAreTheGoodsEightAndTen()
    {
        var castle = Stocked((8, 53), (10, 28));

        Assert.AreEqual(53, castle.Planks);
        Assert.AreEqual(28, castle.Stones);
        Assert.AreEqual(53, castle.Count(new Good { Code = 8 }));
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
}
