using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class NameTableTests
{
    [TestMethod]
    public void AKnownCodeGetsItsName()
    {
        Assert.AreEqual("tree", NameTable.ObjectName(8));
        Assert.AreEqual("tree", NameTable.ObjectName(0x17));
        Assert.AreEqual("felled-tree", NameTable.ObjectName(0x5d));
        Assert.AreEqual("flag", NameTable.ObjectName(1));
        Assert.AreEqual("castle", NameTable.BuildingName(Building.CastleType));
        Assert.AreEqual("carrier", NameTable.JobName(Settler.CarrierJob));
        Assert.AreEqual("walks-along-road", NameTable.StateName(3));
    }

    [TestMethod]
    public void AnUnknownCodeGetsNoName()
    {
        Assert.IsNull(NameTable.StateName(0x43));
        Assert.IsNull(NameTable.ObjectName(5));
        Assert.IsNull(NameTable.JobName(99));
        Assert.IsNull(NameTable.BuildingName(99));
    }

    [TestMethod]
    public void EveryEntryCitesItsEvidence()
    {
        Assert.IsTrue(NameTable.Entries.Any());
        foreach (var entry in NameTable.Entries)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Evidence), $"{entry.Kind} {entry.Code} {entry.Name}");
        }
    }

    [TestMethod]
    public void ANameResolvesToEveryCodeItNames()
    {
        CollectionAssert.AreEqual(Enumerable.Range(8, 16).ToArray(), NameTable.CodesNamed("tree").Take(16).ToArray());
        Assert.AreEqual(16, NameTable.CodesNamed("tree").Count());
        Assert.AreEqual(10, NameTable.CodesNamed("felled-tree").Count());
        Assert.IsFalse(NameTable.CodesNamed("no-such-name").Any());
    }

    [TestMethod]
    public void AStoneIsACodeOfClassTwo()
    {
        var classes = new byte[ProgramTables.ClassCount];
        classes[0x48] = 2;
        classes[0x49] = 0;
        var tables = new ProgramTables(classes, ZeroStock());
        Assert.AreEqual("stone", NameTable.ObjectName(0x48, tables));
        Assert.IsNull(NameTable.ObjectName(0x49, tables));
        Assert.AreEqual("tree", NameTable.ObjectName(8, tables));
    }

    private static byte[][] ZeroStock() =>
        Enumerable.Range(0, ProgramTables.StartStockRows).Select(_ => new byte[StartStock.Goods]).ToArray();
}
