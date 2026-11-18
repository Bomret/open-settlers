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
        var tables = new RuleTables(new ObjectClass[RuleTables.ClassCount], TestProgram.MadeStartStock());

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

        var tables = new RuleTables(classes, TestProgram.MadeStartStock());

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
            TestProgram.MadeStartStock(),
            walkRows: rows
        );

        Assert.AreEqual(0, tables.WalkDirection(2, 0), "4 is east");
        Assert.AreEqual(5, tables.WalkDirection(2, 1), "24 is north");
        Assert.AreEqual(-1, tables.WalkDirection(2, 2), "6 is no multiple of 4");
        Assert.AreEqual(-1, tables.WalkDirection(2, 3), "28 is past north");
        Assert.AreEqual(-1, tables.WalkDirection(0, 0), "an empty row names none");
    }

    // Facts chosen against the original's rules (the log's handover counts low here, coal counts against resource 1), so a rule that
    // derives a fact from a good's code, not from its entry, reads a wrong value.
    static RuleTables WithGoods() =>
        new(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.MadeStartStock(),
            catalog: new ContentCatalog([
                new GoodEntry
                {
                    Name = "log",
                    OriginalCode = 7,
                    Request = new GoodClass { Class = 2, Word = RequestWord.Second },
                    Counter = FillCounter.Low,
                    HandoverCounter = FillCounter.Low,
                    CountsAgainstResource1 = true,
                    TransportRank = 4,
                },
                new GoodEntry
                {
                    Name = "ai-token",
                    Counter = FillCounter.None,
                    HandoverCounter = FillCounter.High,
                    CountsAgainstResource1 = false,
                    TransportRank = 9,
                },
                new GoodEntry
                {
                    Name = "coal",
                    OriginalCode = 13,
                    Counter = FillCounter.High,
                    HandoverCounter = FillCounter.Low,
                    CountsAgainstResource1 = false,
                    TransportRank = 1,
                },
            ])
        );

    [TestMethod]
    public void TheTransportPriorityIsTheRankOfTheGoodsEntryAndZeroForNoGood()
    {
        var tables = WithGoods();

        Assert.AreEqual(4, tables.TransportPriority(Good.Log));
        Assert.AreEqual(9, tables.TransportPriority(new Good { Name = "ai-token" }));
        Assert.AreEqual(0, tables.TransportPriority(Good.None));
    }

    [TestMethod]
    public void TheClassOfAGoodIsTheRequestOfItsEntryAndNullForAGoodForStorage()
    {
        var tables = WithGoods();

        Assert.AreEqual(new GoodClass { Class = 2, Word = RequestWord.Second }, tables.ClassOfGood(Good.Log));
        Assert.IsNull(tables.ClassOfGood(TestCatalog.GoodOf(13)), "an entry with no request goes to storage at once");
        Assert.IsNull(tables.ClassOfGood(Good.None));
    }

    [TestMethod]
    public void EveryRuleFactOfAGoodTheCatalogLacksStopsWithAnErrorNamingIt()
    {
        var tables = WithGoods();
        var lacking = Good.Plank;

        foreach (
            var read in new Action[]
            {
                () => tables.TransportPriority(lacking),
                () => tables.ClassOfGood(lacking),
                () => tables.FillCounterOf(lacking),
                () => tables.HandoverCounterOf(lacking),
                () => tables.CountsAgainstResource1(lacking),
            }
        )
        {
            var error = Assert.ThrowsExactly<InvalidOperationException>(read);
            StringAssert.Contains(error.Message, "plank");
        }
    }

    [TestMethod]
    public void ALostGoodCountsAgainstTheByteItsEntryNames()
    {
        var tables = WithGoods();

        Assert.AreEqual(FillCounter.Low, tables.FillCounterOf(Good.Log));
        Assert.AreEqual(FillCounter.High, tables.FillCounterOf(TestCatalog.GoodOf(13)));
        Assert.AreEqual(FillCounter.None, tables.FillCounterOf(new Good { Name = "ai-token" }));
    }

    [TestMethod]
    public void TheHandoverFillsTheCounterOfTheGoodsEntryWhateverItsCode()
    {
        var tables = WithGoods();

        Assert.AreEqual(FillCounter.Low, tables.HandoverCounterOf(Good.Log), "the original's rule would say high");
        Assert.AreEqual(FillCounter.Low, tables.HandoverCounterOf(TestCatalog.GoodOf(13)));
        Assert.AreEqual(FillCounter.High, tables.HandoverCounterOf(new Good { Name = "ai-token" }));
    }

    [TestMethod]
    public void ALostGoodCountsAgainstResource1WhereItsEntryStatesIt()
    {
        var tables = WithGoods();

        Assert.IsTrue(tables.CountsAgainstResource1(Good.Log), "the original's rule would say no");
        Assert.IsFalse(tables.CountsAgainstResource1(TestCatalog.GoodOf(13)), "coal counts against nothing");
        Assert.IsFalse(tables.CountsAgainstResource1(Good.None));
    }

    [TestMethod]
    public void ABuildingTypeTakesTheSizeAndTheNeedsOfItsTableEntries()
    {
        var sizes = new byte[TestCatalog.BuildingSlots];
        sizes[TestCatalog.CodeOf(BuildingType.Mill)] = 1;
        sizes[TestCatalog.CodeOf(BuildingType.Baker)] = 2;
        var needs = new byte[2 * TestCatalog.BuildingSlots];
        needs[2 * TestCatalog.CodeOf(BuildingType.Baker)] = 7;
        needs[2 * TestCatalog.CodeOf(BuildingType.Baker) + 1] = 3;

        var tables = new RuleTables(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.MadeStartStock(),
            catalog: TestCatalog.StandIn(TestCatalog.Buildings(sizes: sizes, needs: needs))
        );

        Assert.AreEqual(BuildingSize.Mine, tables.SizeOf(BuildingType.Farm));
        Assert.AreEqual(BuildingSize.Small, tables.SizeOf(BuildingType.Mill));
        Assert.AreEqual(BuildingSize.Large, tables.SizeOf(BuildingType.Baker));
        Assert.AreEqual(new BuildingNeeds { Planks = 7, Stones = 3 }, tables.NeedsOf(BuildingType.Baker));
        Assert.AreEqual(new BuildingNeeds { Planks = 0, Stones = 0 }, tables.NeedsOf(BuildingType.Mill));
    }

    [TestMethod]
    public void TheCastleAndNoBuildingAreNoSizeAndNeedNothing()
    {
        var tables = TestProgram.Made();

        Assert.AreEqual(BuildingSize.Mine, tables.SizeOf(BuildingType.Castle));
        Assert.AreEqual(BuildingSize.Mine, tables.SizeOf(BuildingType.None));
        Assert.AreEqual(new BuildingNeeds { Planks = 0, Stones = 0 }, tables.NeedsOf(BuildingType.Castle));
    }

    [TestMethod]
    public void ABuildingTypeTheCatalogLacksStopsTheRulesWithAnErrorNamingIt()
    {
        var tables = new RuleTables(new ObjectClass[RuleTables.ClassCount], TestProgram.MadeStartStock());

        StringAssert.Contains(
            Assert.ThrowsExactly<InvalidOperationException>(() => tables.SizeOf(BuildingType.Sawmill)).Message,
            "sawmill"
        );
        StringAssert.Contains(
            Assert
                .ThrowsExactly<InvalidOperationException>(() => tables.TakesSecondFigure(BuildingType.Sawmill))
                .Message,
            "sawmill"
        );
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
        new(new ObjectClass[RuleTables.ClassCount], TestProgram.MadeStartStock(), jobData: data);

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
            (new Digger(), new WorkerRequest { Job = new Digger(), Tool = TestCatalog.GoodOf(16) }),
            (new Builder(), new WorkerRequest { Job = new Builder(), Tool = TestCatalog.GoodOf(17) })
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
            new WorkerRequest { Job = new Digger(), Tool = TestCatalog.GoodOf(16) },
            tables.Get<WorkerRequest>(new Digger())
        );
        Assert.AreEqual(
            new WorkerRequest { Job = new Builder(), Tool = TestCatalog.GoodOf(17) },
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
    public void ChangingTheGoodsOfADistributionStepAfterConstructionLeavesTheTableUnchanged()
    {
        var goods = new[] { Good.Plank };
        var order = new DistributionStep[][]
        {
            [new() { Class = ConstructionTables.PlankClass, Goods = goods }],
            [],
            [],
        };
        var tables = new RuleTables(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.MadeStartStock(),
            distribution: order
        );

        goods[0] = Good.Stone;

        Assert.AreEqual(Good.Plank, tables.DistributionOrder(2)[0].Goods.Single());
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
