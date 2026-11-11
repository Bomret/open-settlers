using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The typed accessors the rules read in place of the program's bytes. The tables are built here from made-up values.
[TestClass]
public sealed class RuleTablesTests
{
    [TestMethod]
    public void AnObjectClassNamesWhatTheRulesAskOfIt()
    {
        Assert.IsTrue(new ObjectClass(0).IsEmpty);
        Assert.IsTrue(new ObjectClass(1).IsEmpty is false && new ObjectClass(1).IsLarge is false);
        Assert.IsTrue(new ObjectClass(2).IsStone && new ObjectClass(2).IsLarge);
        Assert.IsTrue(new ObjectClass(3).IsFlag && new ObjectClass(3).IsFlagOrBuilding);
        Assert.IsTrue(new ObjectClass(4).IsBuilding && new ObjectClass(4).IsSmallBuilding);
        Assert.IsTrue(new ObjectClass(5).IsLargeBuilding && !new ObjectClass(5).IsCastle);
        Assert.IsTrue(new ObjectClass(6).IsCastle && new ObjectClass(6).IsLargeBuilding);
    }

    [TestMethod]
    public void TheClassOfAnObjectIsTheTablesEntryOfItsCode()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;

        var tables = new RuleTables(classes, TestProgram.Made().StartStock);

        Assert.IsTrue(tables.ClassOf(MapObject.Flag).IsFlag);
        Assert.IsTrue(tables.ClassOf(MapObject.None).IsEmpty);
    }

    [TestMethod]
    public void AWalkRowEntryNamesADirectionOrNone()
    {
        var rows = new byte[RuleTables.WalkRowCount * RuleTables.WalkRowSize];
        rows[RuleTables.WalkRowSize * 2 + 0] = 4;
        rows[RuleTables.WalkRowSize * 2 + 1] = 24;
        rows[RuleTables.WalkRowSize * 2 + 2] = 6;
        rows[RuleTables.WalkRowSize * 2 + 3] = 28;

        var tables = new RuleTables(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.Made().StartStock,
            walkRows: rows
        );

        Assert.AreEqual(0, tables.WalkDirection(2, 0), "4 is east");
        Assert.AreEqual(5, tables.WalkDirection(2, 1), "24 is north");
        Assert.AreEqual(-1, tables.WalkDirection(2, 2), "6 is no multiple of 4");
        Assert.AreEqual(-1, tables.WalkDirection(2, 3), "28 is past north");
        Assert.AreEqual(-1, tables.WalkDirection(0, 0), "an empty row names none");
    }

    [TestMethod]
    public void ALostGoodCountsAgainstTheByteItsKindNames()
    {
        var kinds = new byte[RuleTables.GoodKindCount];
        kinds[3] = 0;
        kinds[4] = 1;
        kinds[5] = 0xff;
        kinds[6] = 2;

        var tables = new RuleTables(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.Made().StartStock,
            goodKinds: kinds
        );

        Assert.AreEqual(FillCounter.Low, tables.FillCounterOf(new Good(3)));
        Assert.AreEqual(FillCounter.High, tables.FillCounterOf(new Good(4)));
        Assert.AreEqual(FillCounter.None, tables.FillCounterOf(new Good(5)));
        Assert.AreEqual(FillCounter.None, tables.FillCounterOf(new Good(6)));
    }

    [TestMethod]
    public void ATransportPriorityIsOnlyDefinedForTheGoodsOneTo26()
    {
        var ranks = new byte[RuleTables.Goods];
        ranks[0] = 26;
        ranks[25] = 1;

        var tables = TestProgram.Made(ranks);

        Assert.AreEqual(26, tables.TransportPriority(new Good(1)));
        Assert.AreEqual(1, tables.TransportPriority(new Good(26)));
        Assert.AreEqual(0, tables.TransportPriority(new Good(0)));
        Assert.AreEqual(0, tables.TransportPriority(new Good(27)));
    }

    [TestMethod]
    public void ABuildingTypeTakesTheSizeAndTheNeedsOfItsTableEntries()
    {
        var sizes = new byte[RuleTables.BuildingTypeCount];
        sizes[5] = 1;
        sizes[6] = 2;
        var needs = new byte[2 * RuleTables.BuildingTypeCount];
        needs[2 * 6] = 7;
        needs[2 * 6 + 1] = 3;

        var tables = new RuleTables(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.Made().StartStock,
            buildingSizes: sizes,
            buildingNeeds: needs
        );

        Assert.AreEqual(BuildingSize.Mine, tables.SizeOf(new BuildingType(4)));
        Assert.AreEqual(BuildingSize.Small, tables.SizeOf(new BuildingType(5)));
        Assert.AreEqual(BuildingSize.Large, tables.SizeOf(new BuildingType(6)));
        Assert.AreEqual(new BuildingNeeds(7, 3), tables.NeedsOf(new BuildingType(6)));
        Assert.AreEqual(new BuildingNeeds(0, 0), tables.NeedsOf(new BuildingType(5)));
    }

    [TestMethod]
    public void ATypeOutsideTheTablesIsNoSizeAndNeedsNothing()
    {
        var tables = TestProgram.Made();

        Assert.AreEqual(BuildingSize.Mine, tables.SizeOf(BuildingType.Castle));
        Assert.AreEqual(BuildingSize.Mine, tables.SizeOf(BuildingType.None));
        Assert.AreEqual(new BuildingNeeds(0, 0), tables.NeedsOf(BuildingType.Castle));
    }

    [TestMethod]
    public void TheTableSizesSpanTheAddressesOfTheirTables()
    {
        var baseSpan = ProgramTablesImporter.FigureOffsetAddress - ProgramTablesImporter.FigureBaseAddress;
        var frameSpan = ProgramTablesImporter.CarriedFigureAddress - ProgramTablesImporter.FrameRowAddress;

        Assert.AreEqual(4 * RuleTables.FigureBaseRowCount, baseSpan);
        Assert.AreEqual(RuleTables.FrameRowCount, frameSpan);
    }
}
