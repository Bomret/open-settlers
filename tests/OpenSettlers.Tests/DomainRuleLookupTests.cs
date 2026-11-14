using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The rule lookups and value types of the domain at their edges. The tables are built here from made-up values that differ
// from one another, so a lookup that reads the wrong entry shows. Each lookup's own result for out-of-table input is pinned;
// the lookups are not made to agree.
[TestClass]
public sealed class DomainRuleLookupTests
{
    // Door speeds: the entry of index i holds 100 + i, so the entry a lookup read names itself.
    static RuleTables DoorTables() =>
        new(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.Made().StartStock,
            doorSpeeds: [.. Enumerable.Range(0, RuleTables.DoorSpeedCount).Select(i => (byte)(100 + i))]
        );

    [TestMethod]
    public void TheDoorSpeedOfNoBuildingIsTheFirstEntry()
    {
        // RuleTables.DoorSpeed: no building reads the first entry (hand arithmetic: 100 + 0).
        Assert.AreEqual((byte)100, DoorTables().DoorSpeed((Building?)null));
    }

    [TestMethod]
    public void TheDoorSpeedOfABuildingUnderConstructionIsTheEntryOfItsTypePlus0x20()
    {
        // docs/rules/build-sites.md, "the bit 0x20 is set while the building is under construction": the forester (type 9)
        // finished reads entry 9 (109), under construction entry 0x29 (hand arithmetic: 100 + 41 = 141).
        var tables = DoorTables();

        Assert.AreEqual((byte)109, tables.DoorSpeed(new Building(BuildingType.Forester, 3, 3, 0)));
        Assert.AreEqual((byte)141, tables.DoorSpeed(new Building(BuildingType.Forester, 3, 3, 0, constructing: true)));
    }

    // A material order whose every bit is set and a blow table, step table, and worker table with a distinct entry for each
    // type, so that only an index outside the table can read as nothing.
    static RuleTables LookupTables() =>
        new(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.Made().StartStock,
            stepTimes: [.. Enumerable.Range(0, RuleTables.StepTimeCount).Select(i => (ushort)(1000 + i))],
            materialOrder: [.. Enumerable.Repeat((ushort)0xffff, RuleTables.BuildingTypeCount)],
            blowSteps: [.. Enumerable.Range(0, 2 * RuleTables.BuildingTypeCount).Select(i => (ushort)(500 + i))],
            workers:
            [
                .. Enumerable.Repeat(
                    new WorkerRequest { Job = new Woodcutter(), Tool = Good.None },
                    RuleTables.BuildingTypeCount
                ),
            ]
        );

    [TestMethod]
    public void StoneAtIsFalseForAStepOutsideZeroToFifteenAndForATypeOutsideTheTable()
    {
        // The order words at 0x43f0ba (docs/rules/construction.md) hold 16 bits, a set bit a stone; every bit is set here, so
        // only the range checks of the lookup can answer false (RuleTables.StoneAt: false outside step 0 to 15 and outside the table).
        var tables = LookupTables();
        var forester = BuildingType.Forester;

        Assert.IsTrue(tables.StoneAt(forester, 0));
        Assert.IsTrue(tables.StoneAt(forester, 15));
        Assert.IsFalse(tables.StoneAt(forester, 16));
        Assert.IsFalse(tables.StoneAt(forester, -1));
        Assert.IsFalse(tables.StoneAt(new BuildingType { Code = RuleTables.BuildingTypeCount }, 0));
        Assert.IsFalse(tables.StoneAt(BuildingType.None, 0));
    }

    [TestMethod]
    public void BlowStepIsZeroForATypeOutsideTheTable()
    {
        // The words at 0x43f0fa are two for each type (docs/rules/construction.md): the forester (9) reads words 18 and 19
        // (hand arithmetic: 500 + 18, 500 + 19); a type outside the table reads 0
        // (RuleTables.BlowStep: 0 for a type outside the table).
        var tables = LookupTables();

        Assert.AreEqual((ushort)518, tables.BlowStep(BuildingType.Forester, late: false));
        Assert.AreEqual((ushort)519, tables.BlowStep(BuildingType.Forester, late: true));
        Assert.AreEqual(
            (ushort)0,
            tables.BlowStep(new BuildingType { Code = RuleTables.BuildingTypeCount }, late: false)
        );
        Assert.AreEqual((ushort)0, tables.BlowStep(BuildingType.None, late: true));
    }

    [TestMethod]
    public void WorkerOfIsNullForATypeOutsideTheTable()
    {
        // The handlers of the jump table 0x42cd1d (docs/rules/build-sites.md) cover the 24 types; every type of the made-up
        // table asks for the woodcutter, so only an index outside the table reads as none
        // (RuleTables.WorkerOf: null for a type whose handler asks for none).
        var tables = LookupTables();

        Assert.IsNotNull(tables.WorkerOf(BuildingType.Forester));
        Assert.IsNull(tables.WorkerOf(new BuildingType { Code = RuleTables.BuildingTypeCount }));
        Assert.IsNull(tables.WorkerOf(BuildingType.None));
    }

    [TestMethod]
    public void StepTimeBelowTheFirstShapeAndPastTheLastReadsAsZero()
    {
        // docs/rules/settlers.md: the table covers the shapes -32 to 127 (StepTimeFirst to 127); entry i holds 1000 + i here
        // (hand arithmetic: shape -32 is entry 0, shape 0 is entry 32);
        // RuleTables.StepTime: a shape outside the table reads as 0.
        var tables = LookupTables();

        Assert.AreEqual((ushort)1000, tables.StepTime(RuleTables.StepTimeFirst));
        Assert.AreEqual((ushort)1032, tables.StepTime(0));
        Assert.AreEqual((ushort)(1000 + RuleTables.StepTimeCount - 1), tables.StepTime(127));
        Assert.AreEqual((ushort)0, tables.StepTime(RuleTables.StepTimeFirst - 1));
        Assert.AreEqual((ushort)0, tables.StepTime(128));
    }

    [TestMethod]
    public void DepositsSkipTypeZeroAndLinkedNodesAndComeInRowMajorOrder()
    {
        // Map.Deposits: row-major node order (index = row * width + column), every node whose resource type is not 0
        // (docs/rules/map-form.md); a node with a link holds no deposit (Node.LinkTo).
        var map = new Map(MapSize.Offered[0]);
        map.At(20, 5).DepositType = 3;
        map.At(20, 5).DepositAmount = 9;
        map.At(2, 5).DepositType = 1;
        map.At(2, 5).DepositAmount = 4;
        map.At(30, 1).DepositType = 2;
        map.At(30, 1).DepositAmount = 7;
        map.At(8, 3).DepositType = 0;
        map.At(8, 3).DepositAmount = 6;
        map.At(9, 3).DepositType = 4;
        map.At(9, 3).DepositAmount = 2;
        map.At(9, 3).LinkTo(12);

        var deposits = map.Deposits;

        CollectionAssert.AreEqual(
            new[]
            {
                new ResourceDeposit
                {
                    X = 30,
                    Y = 1,
                    Type = 2,
                    Amount = 7,
                },
                new ResourceDeposit
                {
                    X = 2,
                    Y = 5,
                    Type = 1,
                    Amount = 4,
                },
                new ResourceDeposit
                {
                    X = 20,
                    Y = 5,
                    Type = 3,
                    Amount = 9,
                },
            },
            deposits.ToArray()
        );
    }

    [TestMethod]
    public void AVectorWrapsEachComponentToASignedByte()
    {
        // The original keeps each in a signed byte (docs/rules/woodcutting.md); hand arithmetic modulo 256 into -128 to 127:
        // 127 stays, 128 is -128, -129 is 127.
        Assert.AreEqual(127, new Vector(127, 0).East);
        Assert.AreEqual(-128, new Vector(128, 0).East);
        Assert.AreEqual(127, new Vector(-129, 0).East);
        Assert.AreEqual(127, new Vector(0, 127).South);
        Assert.AreEqual(-128, new Vector(0, 128).South);
        Assert.AreEqual(127, new Vector(0, -129).South);
    }

    [TestMethod]
    public void TheSpiralEntries256To258AreTheLastRotationsOfTheGroupEntry253Starts()
    {
        // docs/rules/map-form.md, the spiral table: ring r holds 6r entries from 1 + 3r(r - 1), its seeds j = 1 to r - 1 nearest
        // the middle first (|2j - r| smallest, the lower j on a tie), then 0; a group is the turns (x, y) to (x - y, x) of a
        // seed. Hand arithmetic: ring 9 starts at entry 217, its seeds go 4, 5, 3, 6, 2, 7, 1, 8, 0, so the seed (9, 1)
        // starts the group at 217 + 6 * 6 = 253: (9,1), (8,9), (-1,8), (-9,-1), (-8,-9), (1,-8).
        // The count 259 (entry 0 and entries 1 to 258) is docs/rules/settlers.md:360-361, state 0x19 searching entries 1 to 258.
        Assert.AreEqual(259, Spiral.SearchOffsets.Count);
        Assert.AreEqual((9, 1), Spiral.SearchOffsets[253]);
        Assert.AreEqual((-9, -1), Spiral.SearchOffsets[256]);
        Assert.AreEqual((-8, -9), Spiral.SearchOffsets[257]);
        Assert.AreEqual((1, -8), Spiral.SearchOffsets[258]);
    }

    [TestMethod]
    [DataRow(8, TerrainKind.Other)]
    [DataRow(9, TerrainKind.Other)]
    [DataRow(10, TerrainKind.Other)]
    [DataRow(11, TerrainKind.Mountain)]
    [DataRow(12, TerrainKind.Mountain)]
    [DataRow(13, TerrainKind.Mountain)]
    [DataRow(14, TerrainKind.Mountain)]
    [DataRow(15, TerrainKind.Other)]
    public void TerrainCodes8To10And15AreOtherAndCodes11To14AreMountain(int code, TerrainKind kind)
    {
        // docs/rules/build-sites.md:77-78: plain is 4 to 7, mountain 11 to 14, and any other code (8 to 10 and 15) is another kind.
        Assert.AreEqual(kind, new Terrain { Code = code }.Kind);
    }
}
