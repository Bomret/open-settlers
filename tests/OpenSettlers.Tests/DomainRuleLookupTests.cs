using System;
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
    // The made-up speed of the door of no building, and of each type's doors: the forester's standing door speeds 109 and its
    // constructing door 141, every other type's 100 and 140, so the entry a lookup read names itself.
    const byte NoBuildingSpeed = 77;

    static RuleTables DoorTables() =>
        new(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.MadeStartStock(),
            catalog: new ContentCatalog(
                [],
                TestCatalog
                    .Buildings()
                    .Select(entry =>
                        entry with
                        {
                            StandingDoor = entry.Name == BuildingType.Forester.Name ? (byte)109 : (byte)100,
                            ConstructingDoor = entry.Name == BuildingType.Forester.Name ? (byte)141 : (byte)140,
                        }
                    )
            ),
            noBuildingDoor: NoBuildingSpeed
        );

    [TestMethod]
    public void TheDoorSpeedOfNoBuildingIsTheFactOfItsOwn()
    {
        // RuleTables.DoorSpeed: no building reads NoBuildingDoor, which no entry holds.
        Assert.AreEqual(NoBuildingSpeed, DoorTables().DoorSpeed((Building?)null));
    }

    [TestMethod]
    public void TheDoorSpeedOfABuildingUnderConstructionIsTheConstructingSpeedOfItsEntry()
    {
        // docs/rules/build-sites.md, "the bit 0x20 is set while the building is under construction": the entry of the forester
        // states its standing door and its constructing door.
        var tables = DoorTables();

        Assert.AreEqual((byte)109, tables.DoorSpeed(new Building(BuildingType.Forester, 3, 3, 0)));
        Assert.AreEqual((byte)141, tables.DoorSpeed(new Building(BuildingType.Forester, 3, 3, 0, constructing: true)));
    }

    [TestMethod]
    public void TheDoorSpeedOfATypeTheCatalogLacksStopsWithAnErrorNamingIt()
    {
        var tables = new RuleTables(new ObjectClass[RuleTables.ClassCount], TestProgram.MadeStartStock());

        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            tables.DoorSpeed(new Building(BuildingType.Forester, 3, 3, 0))
        );

        StringAssert.Contains(error.Message, "forester-hut");
    }

    // A material order whose every bit is set and a blow table and worker table with a distinct entry for each type, so that only
    // a type the catalog lacks, or no building, can read as nothing.
    static RuleTables LookupTables() =>
        new(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.MadeStartStock(),
            stepTimes: [.. Enumerable.Range(0, RuleTables.StepTimeCount).Select(i => (ushort)(1000 + i))],
            catalog: TestCatalog.StandIn(
                TestCatalog
                    .Buildings(
                        order: [.. Enumerable.Repeat((ushort)0xffff, TestCatalog.BuildingSlots)],
                        blows: [.. Enumerable.Range(0, 2 * TestCatalog.BuildingSlots).Select(i => (ushort)(500 + i))],
                        workers:
                        [
                            .. Enumerable.Repeat(
                                new WorkerRequest { Job = new Woodcutter(), Tool = Good.None },
                                TestCatalog.BuildingSlots
                            ),
                        ]
                    )
                    .Where(entry => entry.Name != BuildingType.Mill.Name)
            )
        );

    [TestMethod]
    public void StoneAtIsFalseForAStepOutsideZeroToFifteenAndForNoBuilding()
    {
        // The order of an entry (0x43f0ba, docs/rules/construction.md) holds 16 turns, a stone where true; every turn is a stone
        // here, so only the range checks of the lookup can answer false (RuleTables.StoneAt: false outside step 0 to 15).
        var tables = LookupTables();
        var forester = BuildingType.Forester;

        Assert.IsTrue(tables.StoneAt(forester, 0));
        Assert.IsTrue(tables.StoneAt(forester, 15));
        Assert.IsFalse(tables.StoneAt(forester, 16));
        Assert.IsFalse(tables.StoneAt(forester, -1));
        Assert.IsFalse(tables.StoneAt(BuildingType.None, 0));
    }

    [TestMethod]
    public void BlowStepReadsTheEarlyAndTheLateStepOfTheEntryAndIsZeroForNoBuilding()
    {
        // The words at 0x43f0fa are two for each type (docs/rules/construction.md): the forester (code 9) has the words 18 and 19
        // (hand arithmetic: 500 + 18, 500 + 19); no building reads 0 (RuleTables.BlowStep).
        var tables = LookupTables();

        Assert.AreEqual((ushort)518, tables.BlowStep(BuildingType.Forester, late: false));
        Assert.AreEqual((ushort)519, tables.BlowStep(BuildingType.Forester, late: true));
        Assert.AreEqual((ushort)0, tables.BlowStep(BuildingType.None, late: true));
    }

    [TestMethod]
    public void WorkerOfIsNullForNoBuildingAndTheRulesStopForATypeTheCatalogLacks()
    {
        // Every type of the made-up catalog asks for the woodcutter but the mill, which the catalog lacks
        // (RuleTables.WorkerOf: null for no building, an error naming a type with no entry).
        var tables = LookupTables();

        Assert.IsNotNull(tables.WorkerOf(BuildingType.Forester));
        Assert.IsNull(tables.WorkerOf(BuildingType.None));
        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidOperationException>(() => tables.WorkerOf(BuildingType.Mill)).Message,
            "mill"
        );
        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidOperationException>(() => tables.SizeOf(BuildingType.Mill)).Message,
            "mill"
        );
        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidOperationException>(() => tables.NeedsOf(BuildingType.Mill)).Message,
            "mill"
        );
        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidOperationException>(() => tables.StoneAt(BuildingType.Mill, 0)).Message,
            "mill"
        );
        StringAssert.Contains(
            Assert
                .ThrowsExactly<InvalidOperationException>(() => tables.BlowStep(BuildingType.Mill, late: false))
                .Message,
            "mill"
        );
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
