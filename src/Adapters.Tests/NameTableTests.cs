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
        Assert.AreEqual("digs", NameTable.StateName(8));
        Assert.AreEqual("walks-along-road", NameTable.StateName(3));
        Assert.AreEqual("builds", NameTable.StateName(9));
        Assert.AreEqual("leaves-site", NameTable.StateName(0x4a));
    }

    [TestMethod]
    public void TheSawmillersJobAndStatesGetTheirNames()
    {
        Assert.AreEqual("saws", NameTable.StateName(0x18));
        Assert.AreEqual("steps-out-with-good", NameTable.StateName(0xb));
        Assert.AreEqual("puts-down-good", NameTable.StateName(0xd));
    }

    [TestMethod]
    public void TheMillersJobAndStateGetTheirNames()
    {
        Assert.AreEqual("grinds", NameTable.StateName(0x23));
    }

    [TestMethod]
    public void TheBakersJobAndStateGetTheirNames()
    {
        Assert.AreEqual("bakes", NameTable.StateName(0x24));
    }

    [TestMethod]
    public void TheButchersJobAndStateGetTheirNames()
    {
        Assert.AreEqual("butchers", NameTable.StateName(0x26));
    }

    [TestMethod]
    public void ThePigFarmersJobAndStateGetTheirNames()
    {
        Assert.AreEqual("tends-pigs", NameTable.StateName(0x25));
    }

    [TestMethod]
    public void TheStonecuttersJobAndStatesGetTheirNames()
    {
        Assert.AreEqual("looks-for-heap", NameTable.StateName(0x15));
        Assert.AreEqual("walks-to-heap", NameTable.StateName(0x16));
        Assert.AreEqual("cuts-stone", NameTable.StateName(0x17));
    }

    [TestMethod]
    public void TheForestersJobStatesAndSaplingsGetTheirNames()
    {
        Assert.AreEqual("looks-for-spot", NameTable.StateName(0x13));
        Assert.AreEqual("plants", NameTable.StateName(0x14));
        var names = Table((0x67, 1), (0x68, 1));
        Assert.AreEqual("sapling", names.ObjectName(MapObject.SaplingCode));
        Assert.AreEqual("sapling", names.ObjectName(MapObject.SecondSaplingCode));
        CollectionAssert.AreEqual(new[] { 0x67, 0x68 }, names.CodesNamed("sapling").ToArray());
    }

    [TestMethod]
    public void TheNameTableHoldsNoBuildingType()
    {
        // A building type takes its name from its catalog entry (ADR 0020), whose name cites its evidence (BuildingTablesTests).
        foreach (var entry in NameTable.Entries)
        {
            Assert.AreNotEqual("castle", entry.Name);
            Assert.AreNotEqual("woodcutter-hut", entry.Name);
            Assert.AreNotEqual("storehouse", entry.Name);
        }
    }

    [TestMethod]
    public void TheNameTableHoldsNoGood()
    {
        // A good takes its name from its catalog entry (ADR 0020); the table names objects, jobs, states, and building types.
        foreach (var entry in NameTable.Entries)
        {
            Assert.AreNotEqual("log", entry.Name);
            Assert.AreNotEqual("shield", entry.Name);
        }

        Assert.IsFalse(NameTable.IsName("fish"));
    }

    [TestMethod]
    public void AnUnknownCodeGetsNoName()
    {
        Assert.IsNull(NameTable.StateName(0x43));
        Assert.IsNull(Table().ObjectName(5));
    }

    [TestMethod]
    public void TheNameTableHoldsNoJob()
    {
        // A job takes its name from its type and its catalog entry (ADR 0020), whose name cites its evidence (JobTests).
        Assert.IsFalse(NameTable.IsName("carrier"));
        Assert.IsFalse(NameTable.IsName("knight-level-0"));
        CollectionAssert.AreEquivalent(
            new[] { NameTable.CodeKind.Object, NameTable.CodeKind.State },
            NameTable.Entries.Select(entry => entry.Kind).Distinct().ToArray()
        );
    }

    [TestMethod]
    public void TheStateNamesTheJobModulesStateAreInTheTableWithTheirEvidence()
    {
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
        // The list the History Edition source declares for the generator is checked against the same codes in that source's
        // test project (the class of this name there); here the table's own states are the ones the modules do not name.
        var modules = JobTableNames.States.Select(state => state.Code).ToHashSet();
        var core = NameTable
            .Entries.Where(entry => entry.Kind == NameTable.CodeKind.State && !modules.Contains(entry.Code))
            .Select(entry => entry.Code);

        CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7, 0xf, 0x10, 0x19, 0x1c, 0x42, 0x4a }, core.ToList());
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
        var names = Table((0x50, 2), (0x1c, 2), (0x6a, 2));
        Assert.AreEqual("rock", names.ObjectName(0x50));
        Assert.IsNull(names.ObjectName(0x1c));
        Assert.AreEqual("field", names.ObjectName(0x6a));
    }

    [TestMethod]
    public void TheHighBitOfACodeIsIgnoredAsTheClassLookupIgnoresIt()
    {
        var names = Table((0x48, 2));
        Assert.AreEqual("stone", names.ObjectName(0xC8));
        Assert.AreEqual("tree", names.ObjectName(0x88));
    }

    static NameTable Table(params (int Code, int Class)[] classes)
    {
        var table = new ObjectClass[RuleTables.ClassCount];
        foreach (var (code, @class) in classes)
        {
            table[code] = (byte)@class;
        }

        return new NameTable(new RuleTables(table, MadeProgram.MadeStartStock()));
    }
}
