using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The exchange of goods between a carrier and the flag it stands on (docs/rules/goods.md), on made-up tables: the emulated games
// of FlagGoodsTests never have a good that goes the way a carrier came, so the swap and the drop are checked here against the
// rule read from 0x43af07 to 0x43b410.
[TestClass]
public sealed class ExchangeTests
{
    private const int Log = 7;
    private const int Stone = 9;
    private const int Way = 3;

    private const int Board = 1;

    private static Game WithFlag(byte[]? transportPriorities = null)
    {
        var game = new Game(
            TestProgram.Made(transportPriorities),
            MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0])
        );
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        return game;
    }

    // A settler of the game's list made a carrier on a flag, with the heading it came in and the good it carries for the flag
    // `destination` (none: no good).
    private static Settler Carrier(Game game, int good, int? destination)
    {
        var settler = game.Settlers[0];
        settler.Activity = new WalksRoad(good == 0 ? null : new Load(new Good(good), destination), new Heading(Way));

        return settler;
    }

    // The good a carrier carries; 0 for none.
    private static int Carried(Settler carrier) => ((WalksRoad)carrier.Activity).Load?.Good.Code ?? 0;

    [TestMethod]
    public void ACarrierThatCarriesAGoodLeavesItInThePlaceOfTheGoodThatWaitsForItsRoadAndTakesThatOneAlong()
    {
        var game = WithFlag();
        game.LayGood(0, 2, Log);
        game.WaitForRoad(0, Way, 2);
        var carrier = Carrier(game, Stone, 0);

        game.Exchange(0, carrier, 0);

        Assert.AreEqual(Log, Carried(carrier), "the carrier takes the good that waited");
        Assert.AreEqual(new Good(Stone), game.Flags[0].Places[2].Good, "the good it carried is left in that place");
        Assert.AreEqual(0, game.Flags[0].Places[2].Destination, "its destination goes with it");
        Assert.IsNull(game.Flags[0].Side(Way).WaitingPlace, "no good waits for the road any more");
        Assert.IsTrue(game.Flags[0].NeedsGoodsTurn, "the flag looks at the new good at its next turn");
    }

    [TestMethod]
    public void ACarrierWithNothingTakesTheGoodThatWaitsForItsRoad()
    {
        var game = WithFlag();
        game.LayGood(0, 5, Log);
        game.WaitForRoad(0, Way, 5);
        var carrier = Carrier(game, 0, null);

        game.Exchange(0, carrier, 0);

        Assert.AreEqual(Log, Carried(carrier));
        Assert.IsTrue(game.Flags[0].Places[5].IsEmpty, "the place is empty");
    }

    [TestMethod]
    public void ACarrierThatCarriesAGoodLeavesItOnTheFirstEmptyPlaceWhereNoGoodWaitsForItsRoad()
    {
        var game = WithFlag();
        game.LayGood(0, 0, Log);
        var carrier = Carrier(game, Stone, 0);

        game.Exchange(0, carrier, 0);

        Assert.AreEqual(0, Carried(carrier), "the carrier has nothing left");
        Assert.AreEqual(new Good(Stone), game.Flags[0].Places[1].Good);
        Assert.AreEqual(0, game.Flags[0].Places[1].Destination);
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
    private static void LayDirected(Game game, int place, int good) => game.LayGood(0, place, good, way: Way);

    [TestMethod]
    public void AfterACarrierTakesAGoodTheGoodOfTheHighestTransportPriorityForTheSameRoadWaitsNext()
    {
        var game = WithFlag(Ranks(log: 5, stone: 9, board: 2));
        LayDirected(game, 0, Board);
        LayDirected(game, 1, Log);
        LayDirected(game, 2, Stone);
        LayDirected(game, 4, Log);
        game.WaitForRoad(0, Way, 4);

        game.Exchange(0, Carrier(game, 0, null), 0);

        Assert.AreEqual(2, game.Flags[0].Side(Way).WaitingPlace, "the stone waits next");
    }

    [TestMethod]
    public void AmongGoodsOfEqualTransportPriorityTheOneOnTheLowestPlaceWaitsNext()
    {
        var game = WithFlag(Ranks(log: 5, stone: 5, board: 2));
        LayDirected(game, 3, Stone);
        LayDirected(game, 1, Log);
        LayDirected(game, 6, Log);
        game.WaitForRoad(0, Way, 6);

        game.Exchange(0, Carrier(game, 0, null), 0);

        Assert.AreEqual(1, game.Flags[0].Side(Way).WaitingPlace);
    }

    [TestMethod]
    public void AGoodWithNoTransportPriorityIsNeverLinedUp()
    {
        var game = WithFlag(Ranks(log: 0, stone: 0, board: 0));
        LayDirected(game, 1, Log);
        LayDirected(game, 6, Stone);
        game.WaitForRoad(0, Way, 6);

        game.Exchange(0, Carrier(game, 0, null), 0);

        Assert.IsNull(game.Flags[0].Side(Way).WaitingPlace, "no good waits for the road");
    }

    [TestMethod]
    public void ACarrierKeepsAGoodWhenAllEightPlacesAreFull()
    {
        var game = WithFlag();
        for (var place = 0; place < 8; place++)
        {
            game.LayGood(0, place, Log);
        }

        var carrier = Carrier(game, Stone, 0);

        game.Exchange(0, carrier, 0);

        Assert.AreEqual(Stone, Carried(carrier));
    }
}
