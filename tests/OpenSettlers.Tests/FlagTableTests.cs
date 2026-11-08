using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The flag table and the road network are typed Domain state (ADR 0013): a flag's slot is its position in the table, a
// freed slot is reused before the table grows, and the table holds 256 flags.
[TestClass]
public sealed class FlagTableTests
{
    [TestMethod]
    public void AFlagTakesTheEndOfTheTableAndKeepsItsNode()
    {
        var table = new FlagTable();

        var slot = table.Add(4, 5);

        Assert.AreEqual(0, slot);
        Assert.AreEqual((4, 5), (table[0].X, table[0].Y));
        Assert.IsFalse(table[0].IsFree);
    }

    [TestMethod]
    public void AFreedSlotInTheMiddleStaysAndTheNextFlagTakesIt()
    {
        var table = new FlagTable();
        table.Add(1, 1);
        table.Add(2, 2);
        table.Add(3, 3);

        table.Free(1);

        Assert.AreEqual(3, table.Count);
        Assert.IsTrue(table[1].IsFree);
        Assert.AreEqual(1, table.Add(9, 9));
        Assert.AreEqual((9, 9), (table[1].X, table[1].Y));
    }

    [TestMethod]
    public void TheCountFallsPastTheFreeSlotsAtTheEnd()
    {
        var table = new FlagTable();
        table.Add(1, 1);
        table.Add(2, 2);
        table.Add(3, 3);
        table.Free(1);

        table.Free(2);

        Assert.AreEqual(1, table.Count);
    }

    [TestMethod]
    public void ATableOf256FlagsIsFullAndOneWithAFreeSlotIsNot()
    {
        var table = new FlagTable();
        for (var i = 0; i < FlagTable.Capacity - 1; i++)
        {
            table.Add(i, 0);
        }

        Assert.IsFalse(table.IsFull);

        table.Add(255, 0);

        Assert.IsTrue(table.IsFull);

        table.Free(7);

        Assert.IsFalse(table.IsFull);
    }

    [TestMethod]
    public void TheRoadNetworkFindsTheRoadThatLeavesAFlagAndItsFarEnd()
    {
        var network = new RoadNetwork();
        var road = new Road(0, 3, 2, 0, [3, 3], false);
        network.Add(road);

        Assert.AreSame(road, network.RoadAt(0, 3));
        Assert.AreSame(road, network.RoadAt(2, 0));
        Assert.IsNull(network.RoadAt(0, 0));
        Assert.AreEqual((2, 0), RoadNetwork.FarEnd(road, 0, 3));
        Assert.AreEqual((0, 3), RoadNetwork.FarEnd(road, 2, 0));
        Assert.AreEqual(2, network.FlagNeighbour(0, 3));
        Assert.AreEqual(-1, network.FlagNeighbour(0, 4));
    }

    [TestMethod]
    public void TheRoadNetworkListsItsRoadsInTheOrderTheyWereAddedAndForgetsARemovedOne()
    {
        var network = new RoadNetwork();
        var first = new Road(0, 3, 1, 0, [3], false);
        var second = new Road(1, 1, 2, 4, [1], false);
        network.Add(first);
        network.Add(second);

        network.Remove(first);

        CollectionAssert.AreEqual(new[] { second }, network.Roads.ToArray());
    }
}
