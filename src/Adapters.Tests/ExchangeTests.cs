using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The exchange of goods between a carrier and the flag it stands on (docs/rules/goods.md), on made-up tables: the emulated games
// of FlagGoodsTests never have a good that goes the way a carrier came, so the swap and the drop are checked here against the
// rule read from 0x43af07 to 0x43b410. The goods behaviour is built on a hand-made flag table.
[TestClass]
public sealed class ExchangeTests
{
    const int Log = 7;
    const int Ninth = 9;
    const int Way = 3;

    const int Board = 1;

    // The flag table holds the castle's flag alone.
    static (BehaviourBench Bench, GoodsTransport Goods) WithFlag(byte[]? transportPriorities = null)
    {
        var bench = new BehaviourBench(transportPriorities);
        bench.Flags.Add(15, 5);

        return (bench, new GoodsTransport(bench.Movement, bench.Flags, bench.Roads, bench.History));
    }

    // A settler made a carrier on a flag, with the heading it came in and the good it carries for the flag `destination` (none:
    // no good).
    static Settler Carrier(BehaviourBench bench, int good, int? destination) =>
        bench.Settlers[
            bench.Place(
                new Settler(
                    new Carrier(),
                    0,
                    0,
                    0,
                    0,
                    new WalksRoad
                    {
                        Load =
                            good == 0
                                ? null
                                : new Load
                                {
                                    Good = new Good { Code = good },
                                    Destination = destination,
                                },
                        Heading = new Heading { Value = Way },
                    }
                )
            )
        ];

    static void Exchange(GoodsTransport goods, Settler carrier) =>
        goods.Exchange(0, carrier, (WalksRoad)carrier.Activity, 0);

    // The good a carrier carries; 0 for none.
    static int Carried(Settler carrier) => ((WalksRoad)carrier.Activity).Load?.Good.Code ?? 0;

    [TestMethod]
    public void ACarrierThatCarriesAGoodLeavesItInThePlaceOfTheGoodThatWaitsForItsRoadAndTakesThatOneAlong()
    {
        var (bench, goods) = WithFlag();
        goods.LayGood(0, 2, Log);
        goods.WaitForRoad(0, Way, 2);
        var carrier = Carrier(bench, Ninth, 0);

        Exchange(goods, carrier);

        Assert.AreEqual(Log, Carried(carrier), "the carrier takes the good that waited");
        Assert.AreEqual(
            new Good { Code = Ninth },
            bench.Flags[0].Places[2].Good,
            "the good it carried is left in that place"
        );
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

        Exchange(goods, carrier);

        Assert.AreEqual(Log, Carried(carrier));
        Assert.IsTrue(bench.Flags[0].Places[5].IsEmpty, "the place is empty");
    }

    [TestMethod]
    public void ACarrierThatCarriesAGoodLeavesItOnTheFirstEmptyPlaceWhereNoGoodWaitsForItsRoad()
    {
        var (bench, goods) = WithFlag();
        goods.LayGood(0, 0, Log);
        var carrier = Carrier(bench, Ninth, 0);

        Exchange(goods, carrier);

        Assert.AreEqual(0, Carried(carrier), "the carrier has nothing left");
        Assert.AreEqual(new Good { Code = Ninth }, bench.Flags[0].Places[1].Good);
        Assert.AreEqual(0, bench.Flags[0].Places[1].Destination);
    }

    // Made-up ranks for the goods 1 to 26 (index good - 1): the ninth ranks above the log, the board below it.
    static byte[] Ranks(int log, int ninth, int board)
    {
        var ranks = new byte[StartStock.Goods];
        ranks[Log - 1] = (byte)log;
        ranks[Ninth - 1] = (byte)ninth;
        ranks[Board - 1] = (byte)board;

        return ranks;
    }

    // A good on a place that has been given the road's direction (bits 5 to 7 hold the direction plus one).
    static void LayDirected(GoodsTransport goods, int place, int good) => goods.LayGood(0, place, good, way: Way);

    [TestMethod]
    public void AfterACarrierTakesAGoodTheGoodOfTheHighestTransportPriorityForTheSameRoadWaitsNext()
    {
        var (bench, goods) = WithFlag(Ranks(log: 5, ninth: 9, board: 2));
        LayDirected(goods, 0, Board);
        LayDirected(goods, 1, Log);
        LayDirected(goods, 2, Ninth);
        LayDirected(goods, 4, Log);
        goods.WaitForRoad(0, Way, 4);

        Exchange(goods, Carrier(bench, 0, null));

        Assert.AreEqual(2, bench.Flags[0].Side(Way).WaitingPlace, "the ninth waits next");
    }

    [TestMethod]
    public void AmongGoodsOfEqualTransportPriorityTheOneOnTheLowestPlaceWaitsNext()
    {
        var (bench, goods) = WithFlag(Ranks(log: 5, ninth: 5, board: 2));
        LayDirected(goods, 3, Ninth);
        LayDirected(goods, 1, Log);
        LayDirected(goods, 6, Log);
        goods.WaitForRoad(0, Way, 6);

        Exchange(goods, Carrier(bench, 0, null));

        Assert.AreEqual(1, bench.Flags[0].Side(Way).WaitingPlace);
    }

    [TestMethod]
    public void AGoodWithNoTransportPriorityIsNeverLinedUp()
    {
        var (bench, goods) = WithFlag(Ranks(log: 0, ninth: 0, board: 0));
        LayDirected(goods, 1, Log);
        LayDirected(goods, 6, Ninth);
        goods.WaitForRoad(0, Way, 6);

        Exchange(goods, Carrier(bench, 0, null));

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

        var carrier = Carrier(bench, Ninth, 0);

        Exchange(goods, carrier);

        Assert.AreEqual(Ninth, Carried(carrier));
    }
}
