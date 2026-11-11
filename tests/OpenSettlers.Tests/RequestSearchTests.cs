using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The request search of a flag's turn (0x464364 to 0x46444c and 0x4644ab to 0x464a3f, docs/rules/construction-materials.md): a good with
// no destination whose class the table names looks over the roads that hold a carrier for the flag whose request asks for its class, the
// highest rank first, before it goes to storage. Flags and sites made by hand; the order the program picks is in
// ConstructionMaterialsTests.
[TestClass]
public sealed class RequestSearchTests
{
    private const int East = 0;
    private const int West = 3;

    // Flags 0 (the castle's), 1, 2, and 3 in a line of roads that hold a carrier; the flags 2 and 3 are the flags of building sites.
    private static (BehaviourBench Bench, GoodsTransport Goods, Building Near, Building Far) Line(
        byte nearRank,
        byte farRank,
        bool carriers = true
    )
    {
        var bench = new BehaviourBench(ConstructionTables.Tables());
        for (var column = 0; column < 4; column++)
        {
            bench.Flags.Add(4 + 2 * column, 4);
        }

        bench.Join(0, East, 1, West, carrier: carriers);
        bench.Join(1, East, 2, West, carrier: carriers);
        bench.Join(2, East, 3, West, carrier: carriers);
        var goods = new GoodsTransport(bench.Movement, bench.Flags, bench.Roads, bench.History, bench.Buildings);
        var near = Site(bench, 2, nearRank);
        var far = Site(bench, 3, farRank);

        return (bench, goods, near, far);
    }

    private static Building Site(BehaviourBench bench, int flag, byte rank)
    {
        bench.Flags[flag].SetRequest(RequestWord.First, new Request(ConstructionTables.PlankClass, rank));
        bench.Flags[flag].SetRequest(RequestWord.Second, new Request(ConstructionTables.StoneClass, 0));
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
        bench.Buildings.Add(site);

        return site;
    }

    private static void Turn(GoodsTransport goods, int flag) => goods.Turn(flag);

    [TestMethod]
    public void APlankGoesToTheFlagThatAsksForPlanksAheadOfStorage()
    {
        var (bench, goods, near, _) = Line(0x3e, 0);
        goods.LayGood(1, 0, Good.Plank.Code);

        Turn(goods, 1);

        Assert.AreEqual(2, bench.Flags[1].Places[0].Destination, "the site's flag, not the castle's");
        Assert.AreEqual(1, near.FillLow, "the plank is on the way to the site");
        Assert.AreEqual(0, bench.Flags[2].RequestOf(RequestWord.First).Rank, "a request that was served keeps no rank");
    }

    [TestMethod]
    public void AStoneIsLookedForInTheSecondWordOnly()
    {
        var (bench, goods, near, _) = Line(0x3e, 0);
        bench.Flags[2].SetRequest(RequestWord.Second, new Request(ConstructionTables.StoneClass, 0x3e));
        goods.LayGood(1, 0, Good.Stone.Code);

        Turn(goods, 1);

        Assert.AreEqual(2, bench.Flags[1].Places[0].Destination);
        Assert.AreEqual(0, near.FillLow);
        Assert.AreEqual(1, near.FillHigh, "the stone is counted in the high fill byte");
    }

    [TestMethod]
    public void AGoodThatNoFlagAsksForGoesToStorage()
    {
        var (bench, goods, near, far) = Line(0, 0);
        goods.LayGood(1, 0, Good.Plank.Code);

        Turn(goods, 1);

        Assert.AreEqual(0, bench.Flags[1].Places[0].Destination, "the castle's flag");
        Assert.AreEqual(0, near.FillLow + far.FillLow);
    }

    [TestMethod]
    public void ALogHasNoRequestInTheseTablesAndGoesToStorage()
    {
        var (bench, goods, _, _) = Line(0x3e, 0x3e);
        goods.LayGood(1, 0, Good.Log.Code);

        Turn(goods, 1);

        Assert.AreEqual(0, bench.Flags[1].Places[0].Destination, "a log is asked for by class 5, which no site asks");
    }

    [TestMethod]
    public void ARequestOverARoadWithNoCarrierIsNotReached()
    {
        var (bench, goods, near, _) = Line(0x3e, 0x3e, carriers: false);
        goods.LayGood(1, 0, Good.Plank.Code);

        Turn(goods, 1);

        Assert.IsNull(bench.Flags[1].Places[0].Destination, "no road holds a carrier: the good waits");
        Assert.AreEqual(0, near.FillLow);
    }

    [TestMethod]
    public void OfTwoRequestsOfTheSameLevelTheHigherRankIsServed()
    {
        var bench = new BehaviourBench(ConstructionTables.Tables());
        for (var column = 0; column < 3; column++)
        {
            bench.Flags.Add(4 + 2 * column, 4);
        }

        bench.Join(1, East, 0, West);
        bench.Join(1, 1, 2, 4);
        var goods = new GoodsTransport(bench.Movement, bench.Flags, bench.Roads, bench.History, bench.Buildings);
        var low = Site(bench, 0, 0x1e);
        var high = Site(bench, 2, 0x3e);
        goods.LayGood(1, 0, Good.Plank.Code);

        Turn(goods, 1);

        Assert.AreEqual(2, bench.Flags[1].Places[0].Destination);
        Assert.AreEqual(1, high.FillLow);
        Assert.AreEqual(0, low.FillLow);
    }

    [TestMethod]
    public void ADistantRequestMustRankAboveWhatTheNearerOneHasGrownTo()
    {
        // The nearer request (0x1e) is found a level before the farther. Each level the best rank so far grows by a quarter
        // plus one (0x1e + 8 = 0x26): a farther request wins only above that.
        var (bench, goods, near, far) = Line(0x1e, 0x24);
        goods.LayGood(1, 0, Good.Plank.Code);

        Turn(goods, 1);

        Assert.AreEqual(2, bench.Flags[1].Places[0].Destination, "0x24 does not beat the grown 0x26");
        Assert.AreEqual(1, near.FillLow);
        Assert.AreEqual(0, far.FillLow);

        var (bench2, goods2, near2, far2) = Line(0x1e, 0x28);
        goods2.LayGood(1, 0, Good.Plank.Code);

        Turn(goods2, 1);

        Assert.AreEqual(3, bench2.Flags[1].Places[0].Destination, "0x28 beats the grown 0x26");
        Assert.AreEqual(0, near2.FillLow);
        Assert.AreEqual(1, far2.FillLow);
    }

    [TestMethod]
    public void AGoodThatCannotReachItsRequestGivesItUpAndTheSiteAsksAgain()
    {
        var (bench, goods, near, _) = Line(0x3e, 0);
        goods.LayGood(1, 0, Good.Plank.Code);
        Turn(goods, 1);
        Assert.AreEqual(1, near.FillLow);

        // The road to the site loses its carrier: the next turn cannot reach the destination and releases the request.
        bench.Flags[1].Side(East).HoldsCarrier = false;
        bench.Flags[2].Side(West).HoldsCarrier = false;
        Turn(goods, 1);

        Assert.IsNull(bench.Flags[1].Places[0].Destination);
        Assert.AreEqual(0, near.FillLow, "the plank no longer counts as on the way");
    }
}
