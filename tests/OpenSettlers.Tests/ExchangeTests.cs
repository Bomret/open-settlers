using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The exchange of goods between a carrier and the flag it stands on (docs/rules/goods.md), on made-up tables: the emulated games
// of FlagGoodsTests never have a good that goes the way a carrier came, so the swap and the drop are checked here against the
// rule read from 0x43af07 to 0x43b410. The goods behaviour is built on a hand-made flag table.
[TestClass]
public sealed class ExchangeTests
{
    private const int Log = 7;
    private const int Stone = 9;
    private const int Way = 3;

    private const int Board = 1;

    // The flag table holds the castle's flag alone.
    private static (BehaviourBench Bench, GoodsTransport Goods) WithFlag(byte[]? transportPriorities = null)
    {
        var bench = new BehaviourBench(transportPriorities);
        bench.Flags.Add(15, 5);

        return (bench, new GoodsTransport(bench.Movement, bench.Flags, bench.Roads, bench.History));
    }

    // A settler made a carrier on a flag, with the heading it came in and the good it carries for the flag `destination` (none:
    // no good).
    private static Settler Carrier(BehaviourBench bench, int good, int? destination) =>
        bench.Settlers[
            bench.Place(
                new Settler(
                    Job.Carrier,
                    0,
                    0,
                    0,
                    0,
                    new WalksRoad(good == 0 ? null : new Load(new Good(good), destination), new Heading(Way))
                )
            )
        ];

    // The good a carrier carries; 0 for none.
    private static int Carried(Settler carrier) => ((WalksRoad)carrier.Activity).Load?.Good.Code ?? 0;

    [TestMethod]
    public void ACarrierThatCarriesAGoodLeavesItInThePlaceOfTheGoodThatWaitsForItsRoadAndTakesThatOneAlong()
    {
        var (bench, goods) = WithFlag();
        goods.LayGood(0, 2, Log);
        goods.WaitForRoad(0, Way, 2);
        var carrier = Carrier(bench, Stone, 0);

        goods.Exchange(0, carrier, 0);

        Assert.AreEqual(Log, Carried(carrier), "the carrier takes the good that waited");
        Assert.AreEqual(new Good(Stone), bench.Flags[0].Places[2].Good, "the good it carried is left in that place");
        Assert.AreEqual(0, bench.Flags[0].Places[2].Destination, "its destination goes with it");
        Assert.IsNull(bench.Flags[0].Side(Way).WaitingPlace, "no good waits for the road any more");
        Assert.IsTrue(bench.Flags[0].NeedsGoodsTurn, "the flag looks at the new good at its next turn");
    }

    [TestMethod]
    public void ACarrierWithNothingTakesTheGoodThatWaitsForItsRoad()
    {
        var (bench, goods) = WithFlag();
        goods.LayGood(0, 5, Log);
        goods.WaitForRoad(0, Way, 5);
        var carrier = Carrier(bench, 0, null);

        goods.Exchange(0, carrier, 0);

        Assert.AreEqual(Log, Carried(carrier));
        Assert.IsTrue(bench.Flags[0].Places[5].IsEmpty, "the place is empty");
    }

    [TestMethod]
    public void ACarrierThatCarriesAGoodLeavesItOnTheFirstEmptyPlaceWhereNoGoodWaitsForItsRoad()
    {
        var (bench, goods) = WithFlag();
        goods.LayGood(0, 0, Log);
        var carrier = Carrier(bench, Stone, 0);

        goods.Exchange(0, carrier, 0);

        Assert.AreEqual(0, Carried(carrier), "the carrier has nothing left");
        Assert.AreEqual(new Good(Stone), bench.Flags[0].Places[1].Good);
        Assert.AreEqual(0, bench.Flags[0].Places[1].Destination);
    }

    // Made-up ranks for the goods 1 to 26 (index good - 1): the stone ranks above the log, the board below it.
    private static byte[] Ranks(int log, int stone, int board)
    {
        var ranks = new byte[StartStock.Goods];
        ranks[Log - 1] = (byte)log;
        ranks[Stone - 1] = (byte)stone;
        ranks[Board - 1] = (byte)board;

        return ranks;
    }

    // A good on a place that has been given the road's direction (bits 5 to 7 hold the direction plus one).
    private static void LayDirected(GoodsTransport goods, int place, int good) => goods.LayGood(0, place, good, way: Way);

    [TestMethod]
    public void AfterACarrierTakesAGoodTheGoodOfTheHighestTransportPriorityForTheSameRoadWaitsNext()
    {
        var (bench, goods) = WithFlag(Ranks(log: 5, stone: 9, board: 2));
        LayDirected(goods, 0, Board);
        LayDirected(goods, 1, Log);
        LayDirected(goods, 2, Stone);
        LayDirected(goods, 4, Log);
        goods.WaitForRoad(0, Way, 4);

        goods.Exchange(0, Carrier(bench, 0, null), 0);

        Assert.AreEqual(2, bench.Flags[0].Side(Way).WaitingPlace, "the stone waits next");
    }

    [TestMethod]
    public void AmongGoodsOfEqualTransportPriorityTheOneOnTheLowestPlaceWaitsNext()
    {
        var (bench, goods) = WithFlag(Ranks(log: 5, stone: 5, board: 2));
        LayDirected(goods, 3, Stone);
        LayDirected(goods, 1, Log);
        LayDirected(goods, 6, Log);
        goods.WaitForRoad(0, Way, 6);

        goods.Exchange(0, Carrier(bench, 0, null), 0);

        Assert.AreEqual(1, bench.Flags[0].Side(Way).WaitingPlace);
    }

    [TestMethod]
    public void AGoodWithNoTransportPriorityIsNeverLinedUp()
    {
        var (bench, goods) = WithFlag(Ranks(log: 0, stone: 0, board: 0));
        LayDirected(goods, 1, Log);
        LayDirected(goods, 6, Stone);
        goods.WaitForRoad(0, Way, 6);

        goods.Exchange(0, Carrier(bench, 0, null), 0);

        Assert.IsNull(bench.Flags[0].Side(Way).WaitingPlace, "no good waits for the road");
    }

    [TestMethod]
    public void ACarrierKeepsAGoodWhenAllEightPlacesAreFull()
    {
        var (bench, goods) = WithFlag();
        for (var place = 0; place < 8; place++)
        {
            goods.LayGood(0, place, Log);
        }

        var carrier = Carrier(bench, Stone, 0);

        goods.Exchange(0, carrier, 0);

        Assert.AreEqual(Stone, Carried(carrier));
    }
}
