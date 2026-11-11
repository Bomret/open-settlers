using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The castle's hand-out of goods to the requests of building sites (the phase routine 0x42965c, docs/rules/construction-materials.md):
// once in each phase cycle the order of the steps is drawn, and for each step the castle hands one good of its stock to the flag whose
// request of the step's class ranks highest, over the roads that hold a carrier. Flags, sites, and a castle made by hand, with made-up
// tables; the program's order and ranks are in ConstructionMaterialsTests.
[TestClass]
public sealed class StorageDistributionTests
{
    private const int East = 0;
    private const int West = 3;
    private const int Planks = 8;
    private const int Stones = 10;

    private sealed record Rig(
        BehaviourBench Bench,
        CastleStock Castle,
        StorageDistribution Distribution,
        GoodsTransport Goods
    );

    // Flag 0 is the castle's; flags 1 and 2 are sites' flags on roads that hold a carrier (flag 2 beyond flag 1). The castle holds
    // the planks and stones given.
    private static Rig Started(int planks, int stones, ushort draw = 2, bool carriers = true)
    {
        var bench = new BehaviourBench(ConstructionTables.Tables(), draw);
        for (var column = 0; column < 3; column++)
        {
            bench.Flags.Add(4 + 2 * column, 4);
        }

        bench.Join(0, East, 1, West, carrier: carriers);
        bench.Join(1, East, 2, West, carrier: carriers);
        var stock = new int[StartStock.Goods];
        stock[Planks - 1] = planks;
        stock[Stones - 1] = stones;
        var castle = new CastleStock();
        castle.Place(new Castle(3, 3, 1), stock);
        var goods = new GoodsTransport(bench.Movement, bench.Flags, bench.Roads, bench.History, bench.Buildings);
        var distribution = new StorageDistribution(
            bench.Tables,
            castle,
            bench.Flags,
            bench.Roads,
            bench.Buildings,
            goods,
            bench.Draws,
            bench.History
        );

        return new Rig(bench, castle, distribution, goods);
    }

    private static Building Site(Rig rig, int flag, byte plankRank, byte stoneRank = 0)
    {
        rig.Bench.Flags[flag].SetRequest(RequestWord.First, new Request(ConstructionTables.PlankClass, plankRank));
        rig.Bench.Flags[flag].SetRequest(RequestWord.Second, new Request(ConstructionTables.StoneClass, stoneRank));
        var site = new Building(BuildingType.Woodcutter, 3 + 2 * flag, 3, flag, constructing: true)
        {
            Progress = 1,
            Site = new BuildingSite
            {
                Planks = 2,
                Stones = 0,
                TargetHeight = 1,
            },
        };
        rig.Bench.Buildings.Add(site);

        return site;
    }

    [TestMethod]
    public void TheCastleHandsAPlankToTheSiteThatAsksAndItsCountFalls()
    {
        var rig = Started(planks: 5, stones: 0);
        var site = Site(rig, 1, 0x3e);

        rig.Distribution.Run();

        Assert.AreEqual(4, rig.Castle.Planks);
        Assert.AreEqual(Planks, rig.Bench.Flags[0].Places[0].Good.Code, "the plank lies on the castle's flag");
        Assert.AreEqual(1, rig.Bench.Flags[0].Places[0].Destination, "bound for the site's flag");
        Assert.AreEqual(1, site.FillLow, "and counted on its way to the site");
        Assert.AreEqual(
            0,
            rig.Bench.Flags[1].RequestOf(RequestWord.First).Rank,
            "the request that was served keeps no rank"
        );
        Assert.IsTrue(rig.Bench.Flags[0].NeedsGoodsTurn, "the flag's turn gives the plank its direction");
    }

    [TestMethod]
    public void WithNoPlankInStockTheRequestStands()
    {
        var rig = Started(planks: 0, stones: 0);
        var site = Site(rig, 1, 0x3e);

        rig.Distribution.Run();

        Assert.AreEqual(0, site.FillLow);
        Assert.AreEqual(0x3e, rig.Bench.Flags[1].RequestOf(RequestWord.First).Rank, "the request is still asked");
        Assert.IsTrue(rig.Bench.Flags[0].Places[0].IsEmpty);
    }

    [TestMethod]
    public void ThePlankThatLaterReachesStorageGoesOutToTheStandingRequest()
    {
        var rig = Started(planks: 0, stones: 0);
        var site = Site(rig, 1, 0x3e);
        rig.Distribution.Run();
        Assert.AreEqual(0, site.FillLow);

        rig.Castle.Add(Good.Plank);
        rig.Bench.Draws.Enqueue(2);
        rig.Distribution.Run();

        Assert.AreEqual(1, site.FillLow);
        Assert.AreEqual(0, rig.Castle.Planks);
    }

    [TestMethod]
    public void OfTwoSitesTheHigherRankIsServedFirstAndTheNextCycleServesTheOther()
    {
        var rig = Started(planks: 5, stones: 0);
        var near = Site(rig, 1, 0x1e);
        var far = Site(rig, 2, 0x3e);

        rig.Distribution.Run();

        Assert.AreEqual(1, far.FillLow, "the higher rank is served though it is farther (0x1e grows to 0x26 only)");
        Assert.AreEqual(0, near.FillLow);

        rig.Bench.Draws.Enqueue(2);
        rig.Distribution.Run();

        Assert.AreEqual(1, near.FillLow, "the request of the nearer site, the only one left, is served next");
        Assert.AreEqual(3, rig.Castle.Planks);
    }

    [TestMethod]
    public void TheDrawPicksTheOrderOfTheStepsAndSoWhichGoodLiesFirstOnTheFlag()
    {
        var planksFirst = Started(planks: 5, stones: 5, draw: 2);
        Site(planksFirst, 1, 0x3e, 0x3e);
        planksFirst.Distribution.Run();

        Assert.AreEqual(Planks, planksFirst.Bench.Flags[0].Places[0].Good.Code);
        Assert.AreEqual(Stones, planksFirst.Bench.Flags[0].Places[1].Good.Code);

        var stonesFirst = Started(planks: 5, stones: 5, draw: 0);
        Site(stonesFirst, 1, 0x3e, 0x3e);
        stonesFirst.Distribution.Run();

        Assert.AreEqual(Stones, stonesFirst.Bench.Flags[0].Places[0].Good.Code);
        Assert.AreEqual(Planks, stonesFirst.Bench.Flags[0].Places[1].Good.Code);
    }

    [TestMethod]
    public void ARequestOverRoadsWithNoCarrierIsNotReachedUntilACarrierComes()
    {
        var rig = Started(planks: 5, stones: 0, carriers: false);
        var site = Site(rig, 1, 0x3e);

        rig.Distribution.Run();

        Assert.AreEqual(5, rig.Castle.Planks);
        Assert.AreEqual(0, site.FillLow);

        // A carrier on the road makes the request reachable: the next cycle serves it.
        rig.Bench.Flags[0].Side(East).HoldsCarrier = true;
        rig.Bench.Flags[1].Side(West).HoldsCarrier = true;
        rig.Bench.Draws.Enqueue(2);
        rig.Distribution.Run();

        Assert.AreEqual(4, rig.Castle.Planks);
        Assert.AreEqual(1, site.FillLow);
    }

    [TestMethod]
    public void ARequestOfRankSixteenOrBelowIsNotServed()
    {
        var rig = Started(planks: 5, stones: 0);
        var site = Site(rig, 1, 0x0e);

        rig.Distribution.Run();

        Assert.AreEqual(5, rig.Castle.Planks);
        Assert.AreEqual(0, site.FillLow);
    }

    [TestMethod]
    public void TheDrawIsTakenInEveryCycleWhetherOrNotAnythingIsAsked()
    {
        var rig = Started(planks: 0, stones: 0);

        rig.Distribution.Run();

        Assert.AreEqual(1, rig.Bench.Draws.Taken);
    }
}
