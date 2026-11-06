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
        var names = Table((8, 1), (0x17, 1), (0x48, 2), (0x53, 1), (0x5d, 1), (1, 3));
        Assert.AreEqual("tree", names.ObjectName(8));
        Assert.AreEqual("tree", names.ObjectName(0x17));
        Assert.AreEqual("felled-tree", names.ObjectName(0x5d));
        Assert.AreEqual("stump", names.ObjectName(0x53));
        Assert.AreEqual("flag", names.ObjectName(1));
        Assert.AreEqual("castle", NameTable.BuildingName(Building.CastleType));
        Assert.AreEqual("storehouse", NameTable.BuildingName(0xa));
        Assert.AreEqual("carrier", NameTable.JobName(Settler.CarrierJob));
        Assert.AreEqual("builder", NameTable.JobName(4));
        Assert.AreEqual("jobless", NameTable.JobName(21));
        Assert.AreEqual("walks-along-road", NameTable.StateName(3));
    }

    [TestMethod]
    public void AnUnknownCodeGetsNoName()
    {
        Assert.IsNull(NameTable.StateName(0x43));
        Assert.IsNull(Table().ObjectName(5));
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
        var names = Table();
        CollectionAssert.AreEqual(Enumerable.Range(8, 16).ToArray(), names.CodesNamed("tree").ToArray());
        Assert.AreEqual(10, names.CodesNamed("felled-tree").Count());
        Assert.IsFalse(names.CodesNamed("no-such-name").Any());
    }

    [TestMethod]
    public void AStoneIsACodeOfClassTwo()
    {
        var names = Table((0x48, 2), (0x49, 0), (0x4a, 1), (8, 1));
        Assert.AreEqual("stone", names.ObjectName(0x48));
        Assert.IsNull(names.ObjectName(0x49));
        Assert.IsNull(names.ObjectName(0x4a));
        Assert.AreEqual("tree", names.ObjectName(8));
    }

    [TestMethod]
    public void TheStonesAreTheCodesTheClassTableCallsClassTwo()
    {
        var names = Table((0x48, 2), (0x4b, 2), (0x49, 3));
        CollectionAssert.AreEqual(new[] { 0x48, 0x4b }, names.CodesNamed("stone").ToArray());
    }

    [TestMethod]
    public void AClassTwoCodeTheDocNamesOtherwiseKeepsItsName()
    {
        var names = Table((0x50, 2), (0x6a, 2));
        Assert.AreEqual("rock", names.ObjectName(0x50));
        Assert.IsNull(names.ObjectName(0x6a));
    }

    [TestMethod]
    public void TheHighBitOfACodeIsIgnoredAsTheClassLookupIgnoresIt()
    {
        var names = Table((0x48, 2));
        Assert.AreEqual("stone", names.ObjectName(0xC8));
        Assert.AreEqual("tree", names.ObjectName(0x88));
    }

    private static NameTable Table(params (int Code, int Class)[] classes)
    {
        var table = new byte[ProgramTables.ClassCount];
        foreach (var (code, @class) in classes)
        {
            table[code] = (byte)@class;
        }

        return new NameTable(new ProgramTables(table, ZeroStock()));
    }

    private static byte[][] ZeroStock() =>
        Enumerable.Range(0, ProgramTables.StartStockRows).Select(_ => new byte[StartStock.Goods]).ToArray();
}
