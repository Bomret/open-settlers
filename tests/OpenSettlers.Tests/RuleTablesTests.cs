using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The typed accessors the rules read in place of the program's bytes. The tables are built here from made-up values.
[TestClass]
public sealed class RuleTablesTests
{
    [TestMethod]
    public void TablesGivenNoJobDataHoldNoneAndAnyJobIsAnError()
    {
        var tables = new RuleTables(new ObjectClass[RuleTables.ClassCount], TestProgram.Made().StartStock);

        Assert.ThrowsExactly<InvalidOperationException>(() => tables.Get<FigureAdds>(new Woodcutter()));
        Assert.ThrowsExactly<InvalidOperationException>(() => tables.Get<BuilderFigureAdds>(Job.Of(99)));
        Assert.ThrowsExactly<InvalidOperationException>(() => tables.Get<WorkerRequest>(new Builder()));
    }

    [TestMethod]
    public void AnObjectClassNamesWhatTheRulesAskOfIt()
    {
        Assert.IsTrue(new ObjectClass { Value = 0 }.IsEmpty);
        Assert.IsTrue(new ObjectClass { Value = 1 }.IsEmpty is false && new ObjectClass { Value = 1 }.IsLarge is false);
        Assert.IsTrue(new ObjectClass { Value = 2 }.IsStone && new ObjectClass { Value = 2 }.IsLarge);
        Assert.IsTrue(new ObjectClass { Value = 3 }.IsFlag && new ObjectClass { Value = 3 }.IsFlagOrBuilding);
        Assert.IsTrue(new ObjectClass { Value = 4 }.IsBuilding && new ObjectClass { Value = 4 }.IsSmallBuilding);
        Assert.IsTrue(new ObjectClass { Value = 5 }.IsLargeBuilding && !new ObjectClass { Value = 5 }.IsCastle);
        Assert.IsTrue(new ObjectClass { Value = 6 }.IsCastle && new ObjectClass { Value = 6 }.IsLargeBuilding);
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

        Assert.AreEqual(FillCounter.Low, tables.FillCounterOf(new Good { Code = 3 }));
        Assert.AreEqual(FillCounter.High, tables.FillCounterOf(new Good { Code = 4 }));
        Assert.AreEqual(FillCounter.None, tables.FillCounterOf(new Good { Code = 5 }));
        Assert.AreEqual(FillCounter.None, tables.FillCounterOf(new Good { Code = 6 }));
    }

    [TestMethod]
    public void ATransportPriorityIsOnlyDefinedForTheGoodsOneTo26()
    {
        var ranks = new byte[RuleTables.Goods];
        ranks[0] = 26;
        ranks[25] = 1;

        var tables = TestProgram.Made(ranks);

        Assert.AreEqual(26, tables.TransportPriority(new Good { Code = 1 }));
        Assert.AreEqual(1, tables.TransportPriority(new Good { Code = 26 }));
        Assert.AreEqual(0, tables.TransportPriority(new Good { Code = 0 }));
        Assert.AreEqual(0, tables.TransportPriority(new Good { Code = 27 }));
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

        Assert.AreEqual(BuildingSize.Mine, tables.SizeOf(new BuildingType { Code = 4 }));
        Assert.AreEqual(BuildingSize.Small, tables.SizeOf(new BuildingType { Code = 5 }));
        Assert.AreEqual(BuildingSize.Large, tables.SizeOf(new BuildingType { Code = 6 }));
        Assert.AreEqual(new BuildingNeeds { Planks = 7, Stones = 3 }, tables.NeedsOf(new BuildingType { Code = 6 }));
        Assert.AreEqual(new BuildingNeeds { Planks = 0, Stones = 0 }, tables.NeedsOf(new BuildingType { Code = 5 }));
    }

    [TestMethod]
    public void ATypeOutsideTheTablesIsNoSizeAndNeedsNothing()
    {
        var tables = TestProgram.Made();

        Assert.AreEqual(BuildingSize.Mine, tables.SizeOf(BuildingType.Castle));
        Assert.AreEqual(BuildingSize.Mine, tables.SizeOf(BuildingType.None));
        Assert.AreEqual(new BuildingNeeds { Planks = 0, Stones = 0 }, tables.NeedsOf(BuildingType.Castle));
    }

    [TestMethod]
    public void TheTableSizesSpanTheAddressesOfTheirTables()
    {
        var baseSpan = ProgramTablesImporter.FigureOffsetAddress - ProgramTablesImporter.FigureBaseAddress;
        var frameSpan = ProgramTablesImporter.CarriedFigureAddress - ProgramTablesImporter.FrameRowAddress;

        Assert.AreEqual(4 * RuleTables.FigureBaseRowCount, baseSpan);
        Assert.AreEqual(RuleTables.FrameRowCount, frameSpan);
    }

    static RuleTables WithJobData(params (Job, object)[] data) =>
        new(new ObjectClass[RuleTables.ClassCount], TestProgram.Made().StartStock, jobData: data);

    [TestMethod]
    public void JobDataIsKeptByJobAndByType()
    {
        var woodcutter = new FigureAdds
        {
            Walk = 1,
            Carrying = 2,
            Cutting = 3,
        };
        var stonecutter = new FigureAdds
        {
            Walk = 4,
            Carrying = 5,
            Cutting = 6,
        };
        var tables = WithJobData(
            (new Woodcutter(), woodcutter),
            (new Stonecutter(), stonecutter),
            (
                new Digger(),
                new FigureAdds
                {
                    Walk = 7,
                    Carrying = 8,
                    Cutting = 9,
                }
            ),
            (
                new Digger(),
                new WorkerRequest
                {
                    Job = new Digger(),
                    Tool = new Good { Code = 16 },
                }
            ),
            (
                new Builder(),
                new WorkerRequest
                {
                    Job = new Builder(),
                    Tool = new Good { Code = 17 },
                }
            )
        );

        Assert.AreEqual(woodcutter, tables.Get<FigureAdds>(new Woodcutter()));
        Assert.AreEqual(stonecutter, tables.Get<FigureAdds>(new Stonecutter()));
        Assert.AreEqual(
            new FigureAdds
            {
                Walk = 7,
                Carrying = 8,
                Cutting = 9,
            },
            tables.Get<FigureAdds>(new Digger())
        );
        Assert.AreEqual(
            new WorkerRequest
            {
                Job = new Digger(),
                Tool = new Good { Code = 16 },
            },
            tables.Get<WorkerRequest>(new Digger())
        );
        Assert.AreEqual(
            new WorkerRequest
            {
                Job = new Builder(),
                Tool = new Good { Code = 17 },
            },
            tables.Get<WorkerRequest>(new Builder())
        );
    }

    [TestMethod]
    public void AJobWhoseDataTheTablesLackIsAnErrorAndNoDefault()
    {
        var tables = WithJobData(
            (
                new Woodcutter(),
                new FigureAdds
                {
                    Walk = 1,
                    Carrying = 2,
                    Cutting = 3,
                }
            )
        );

        var missingJob = Assert.ThrowsExactly<InvalidOperationException>(() =>
            tables.Get<FigureAdds>(new Stonecutter())
        );
        Assert.ThrowsExactly<InvalidOperationException>(() => tables.Get<WorkerRequest>(new Woodcutter()));
        StringAssert.Contains(missingJob.Message, "Stonecutter");
    }

    [TestMethod]
    public void TheMadeUpJobDataHoldsExactlyTheDataTheJobTypesDeclare()
    {
        var declared = TestProgram.Jobs().SelectMany(job => job.Data.Select(type => (job, type)));
        var made = TestProgram.MadeJobData().Select(entry => (entry.Job, entry.Data.GetType()));

        CollectionAssert.AreEquivalent(declared.ToList(), made.ToList());
        Assert.IsTrue(declared.Any(), "the jobs declare data");
        Assert.AreEqual(
            new WorkerRequest { Job = new Builder(), Tool = Good.None },
            TestProgram.Made().Get<WorkerRequest>(new Builder())
        );
    }
}
