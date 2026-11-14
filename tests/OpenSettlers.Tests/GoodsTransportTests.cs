using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The goods turn of a flag (docs/rules/goods.md) on a flag table made by hand: the castle's flag 0, a flag 1 east of it with a
// road that holds a carrier, and goods laid on the flags, without a castle or a game. The destination and the direction of a good
// come from the flag's turns, and of two goods that wait for the same road the one of the higher transport priority goes first.
[TestClass]
public sealed class GoodsTransportTests
{
    const int Log = 7;

    // A good of the made-up ranks (the ninth: the program gives it no class, so it goes to storage at once).
    const int Ninth = 9;
    const int West = 3;
    const int East = 0;

    static (BehaviourBench Bench, GoodsTransport Goods) Chain(byte[]? priorities = null, bool carrier = true)
    {
        var bench = new BehaviourBench(priorities);
        bench.Flags.Add(4, 4);
        bench.Flags.Add(6, 4);
        bench.Join(0, East, 1, West, carrier: carrier);

        return (bench, new GoodsTransport(bench.Movement, bench.Flags, bench.Roads, bench.History));
    }

    [TestMethod]
    public void AGoodLaidOnAFlagIsGivenTheCastlesFlagAndThenTheDirectionOfTheRoadToIt()
    {
        var (bench, goods) = Chain();
        goods.LayGood(1, 3, Log);

        goods.Turn(1);

        Assert.AreEqual(0, bench.Flags[1].Places[3].Destination, "the castle's flag is its destination");
        Assert.IsNull(bench.Flags[1].Places[3].Way);

        goods.Turn(1);

        Assert.AreEqual(West, bench.Flags[1].Places[3].Way, "the road toward the castle");
        Assert.AreEqual(3, bench.Flags[1].Side(West).WaitingPlace, "the good waits for that road");
    }

    [TestMethod]
    public void AGoodOnAFlagWhoseRoadHoldsNoCarrierWaitsWithoutADestination()
    {
        var (bench, goods) = Chain(carrier: false);
        goods.LayGood(1, 0, Log);

        goods.Turn(1);

        Assert.IsNull(bench.Flags[1].Places[0].Destination);
        Assert.IsTrue(bench.Flags[1].NeedsGoodsTurn, "the flag is asked again at its next turn");
    }

    // A flag 2 east of flag 1 whose only road holds a carrier, while the road from flag 1 to the castle's flag holds none: the storage
    // search reaches no storage from flag 2, and the road with a carrier is the one a good takes at once (0x464539).
    static (BehaviourBench Bench, GoodsTransport Goods) CutOff()
    {
        var bench = new BehaviourBench();
        bench.Flags.Add(4, 4);
        bench.Flags.Add(6, 4);
        bench.Flags.Add(8, 4);
        bench.Join(0, East, 1, West, carrier: false);
        bench.Join(1, East, 2, West);

        return (bench, new GoodsTransport(bench.Movement, bench.Flags, bench.Roads, bench.History));
    }

    [TestMethod]
    public void AGoodOnAFlagCutOffFromTheCastleTakesTheRoadThatHoldsACarrierAtOnceAndKeepsNoDestination()
    {
        var (bench, goods) = CutOff();
        goods.LayGood(2, 4, Ninth);

        goods.Turn(2);

        Assert.IsNull(bench.Flags[2].Places[4].Destination, "no storage is reached");
        Assert.AreEqual(West, bench.Flags[2].Places[4].Way, "the road with a carrier");
        Assert.AreEqual(4, bench.Flags[2].Side(West).WaitingPlace, "the good waits for the free road");
        Assert.IsFalse(bench.Flags[2].NeedsGoodsTurn, "the flag is not asked again");
    }

    [TestMethod]
    public void ASecondGoodOnAFlagCutOffFromTheCastleTakesTheDirectionAndLeavesTheTakenRoadToTheFirst()
    {
        var (bench, goods) = CutOff();
        goods.LayGood(2, 4, Ninth);
        goods.LayGood(2, 1, Ninth);

        goods.Turn(2);

        // The places are visited from the last to the first: the good at place 4 takes the road first.
        Assert.AreEqual(4, bench.Flags[2].Side(West).WaitingPlace);
        Assert.AreEqual(West, bench.Flags[2].Places[1].Way, "the second good takes the direction");
        Assert.IsNull(bench.Flags[2].Places[1].Destination);
    }

    [TestMethod]
    public void TheGoodOfTheHigherTransportPriorityTakesTheRoadFromOneThatWaitsForIt()
    {
        var ranks = new byte[StartStock.Goods];
        ranks[Log - 1] = 5;
        ranks[Ninth - 1] = 9;
        var (bench, goods) = Chain(ranks);
        goods.LayGood(1, 6, Log, destination: 0);
        goods.Turn(1);
        Assert.AreEqual(6, bench.Flags[1].Side(West).WaitingPlace, "the log waits for the road");
        goods.LayGood(1, 2, Ninth, destination: 0);

        goods.Turn(1);

        Assert.AreEqual(
            2,
            bench.Flags[1].Side(West).WaitingPlace,
            "the ninth good has the higher rank and takes the place"
        );
        Assert.AreEqual(West, bench.Flags[1].Places[6].Way, "the log keeps its direction and waits");
    }

    [TestMethod]
    public void AGoodOfALowerTransportPriorityLeavesTheRoadToTheOneThatWaitsForIt()
    {
        var ranks = new byte[StartStock.Goods];
        ranks[Log - 1] = 5;
        ranks[Ninth - 1] = 9;
        var (bench, goods) = Chain(ranks);
        goods.LayGood(1, 6, Ninth, destination: 0);
        goods.Turn(1);
        goods.LayGood(1, 2, Log, destination: 0);

        goods.Turn(1);

        Assert.AreEqual(6, bench.Flags[1].Side(West).WaitingPlace);
    }

    [TestMethod]
    public void AGoodForAFlagNoRoadReachesGivesUpItsDestination()
    {
        var (bench, goods) = Chain();
        bench.Flags.Add(10, 10);
        goods.LayGood(1, 0, Log, destination: 2);

        goods.Turn(1);

        Assert.IsNull(bench.Flags[1].Places[0].Destination);
    }

    [TestMethod]
    public void ALayoutPutOfAGoodRefusesANodeWithoutAFlagAndAPlaceThatHoldsAGood()
    {
        var (bench, goods) = Chain();

        Assert.IsNotNull(goods.PlaceGood(9, 9, 0, Log), "no flag stands on the node");
        Assert.IsNull(goods.PlaceGood(6, 4, 0, Log));
        Assert.AreEqual(new Good { Code = Log }, bench.Flags[1].Places[0].Good);
        Assert.IsNotNull(goods.PlaceGood(6, 4, 0, Ninth), "the place holds a good already");
    }
}
