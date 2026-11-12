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
        Assert.AreEqual("castle", NameTable.BuildingName(BuildingType.Castle.Code));
        Assert.AreEqual("storehouse", NameTable.BuildingName(0xa));
        Assert.AreEqual("carrier", NameTable.JobName(Carrier.Number));
        Assert.AreEqual("digger", NameTable.JobName(Digger.Number));
        Assert.AreEqual("digs", NameTable.StateName(8));
        Assert.AreEqual(3, new Builder().Code);
        Assert.AreEqual("builder", NameTable.JobName(3));
        Assert.AreEqual("castle-settler", NameTable.JobName(4));
        Assert.AreEqual("jobless", NameTable.JobName(21));
        Assert.AreEqual("walks-along-road", NameTable.StateName(3));
        Assert.AreEqual("builds", NameTable.StateName(9));
        Assert.AreEqual("leaves-site", NameTable.StateName(0x4a));
    }

    [TestMethod]
    public void TheSawmillersJobAndStatesGetTheirNames()
    {
        Assert.AreEqual("sawmiller", NameTable.JobName(Sawmiller.Number));
        Assert.AreEqual("saws", NameTable.StateName(0x18));
        Assert.AreEqual("steps-out-with-good", NameTable.StateName(0xb));
        Assert.AreEqual("puts-down-good", NameTable.StateName(0xd));
    }

    [TestMethod]
    public void TheStonecuttersJobAndStatesGetTheirNames()
    {
        Assert.AreEqual("stonecutter", NameTable.JobName(Stonecutter.Number));
        Assert.AreEqual("looks-for-heap", NameTable.StateName(0x15));
        Assert.AreEqual("walks-to-heap", NameTable.StateName(0x16));
        Assert.AreEqual("cuts-stone", NameTable.StateName(0x17));
    }

    [TestMethod]
    public void TheForestersJobStatesAndSaplingsGetTheirNames()
    {
        Assert.AreEqual("forester", NameTable.JobName(Forester.Number));
        Assert.AreEqual("looks-for-spot", NameTable.StateName(0x13));
        Assert.AreEqual("plants", NameTable.StateName(0x14));
        var names = Table((0x67, 1), (0x68, 1));
        Assert.AreEqual("sapling", names.ObjectName(MapObject.SaplingCode));
        Assert.AreEqual("sapling", names.ObjectName(MapObject.SecondSaplingCode));
        CollectionAssert.AreEqual(new[] { 0x67, 0x68 }, names.CodesNamed("sapling").ToArray());
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
    public void TheTypeOneHutSettlerHasNoNameAndEveryOtherPortedJobHasOne()
    {
        Assert.IsNull(NameTable.JobName(TypeOneHut.Number));
        foreach (var number in new[] { 0, 2, 3, 4, 5, 6, 7, 8, 21 })
        {
            Assert.IsNotNull(NameTable.JobName(number), $"job {number}");
        }
    }

    [TestMethod]
    public void TheNamesTheJobModulesStateAreInTheTableWithTheirEvidence()
    {
        var jobs = NameTable.Entries.Where(entry => entry.Kind == NameTable.CodeKind.Job).ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "carrier",
                "digger",
                "builder",
                "castle-settler",
                "woodcutter",
                "sawmiller",
                "stonecutter",
                "forester",
                "jobless",
            },
            jobs.Select(entry => entry.Name).ToArray()
        );
        Assert.AreEqual(
            "the code of the Activity record (src/Domain/Activity.cs)",
            NameTable.Entries.Single(entry => entry.Kind == NameTable.CodeKind.State && entry.Code == 0x11).Evidence
        );
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
    public void TheCoreStateCodesTheGeneratorChecksAreTheStatesTheTableNamesItself()
    {
        var listed = typeof(NameTable)
            .Assembly.GetCustomAttributes(typeof(CoreStateCodesAttribute), false)
            .Cast<CoreStateCodesAttribute>()
            .SelectMany(attribute => attribute.Codes);
        var modules = JobTablesRegistry.StateNames.Select(state => state.Code).ToHashSet();
        var core = NameTable
            .Entries.Where(entry => entry.Kind == NameTable.CodeKind.State && !modules.Contains(entry.Code))
            .Select(entry => entry.Code);

        CollectionAssert.AreEquivalent(core.ToList(), listed.ToList());
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
        var table = new ObjectClass[RuleTables.ClassCount];
        foreach (var (code, @class) in classes)
        {
            table[code] = (byte)@class;
        }

        return new NameTable(new RuleTables(table, ZeroStock()));
    }

    private static byte[][] ZeroStock() =>
        Enumerable.Range(0, RuleTables.StartStockRows).Select(_ => new byte[StartStock.Goods]).ToArray();
}
