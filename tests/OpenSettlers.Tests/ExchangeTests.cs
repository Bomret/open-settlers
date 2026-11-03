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

    private static Game WithFlag()
    {
        var game = new Game(
            TestProgram.Made(),
            MapGenerator.Generate(new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0])
        );
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        return game;
    }

    private static Settler Carrier(int good, int target) =>
        new(Settler.CarrierJob, 0, 0, 0, 0, 3, Way) { Mode = good, Target = target };

    [TestMethod]
    public void ACarrierThatCarriesAGoodLeavesItInThePlaceOfTheGoodThatWaitsForItsRoadAndTakesThatOneAlong()
    {
        var game = WithFlag();
        game.LayGood(0, 2, Log);
        game.WaitForRoad(0, Way, 2);
        var carrier = Carrier(Stone, 1);

        game.Exchange(carrier, 0);

        Assert.AreEqual(Log, carrier.Mode, "the carrier takes the good that waited");
        Assert.AreEqual(Stone, game.FlagGoods(0)[2], "the good it carried is left in that place");
        Assert.AreEqual(1, game.FlagDestinations(0)[2], "its destination goes with it");
        Assert.AreEqual(0, game.FlagDirectionBytes(0)[Way] & 0x80, "no good waits for the road any more");
        Assert.IsTrue(game.FlagGoodsBit(0), "the flag looks at the new good at its next turn");
    }

    [TestMethod]
    public void ACarrierWithNothingTakesTheGoodThatWaitsForItsRoad()
    {
        var game = WithFlag();
        game.LayGood(0, 5, Log);
        game.WaitForRoad(0, Way, 5);
        var carrier = Carrier(0, 0);

        game.Exchange(carrier, 0);

        Assert.AreEqual(Log, carrier.Mode);
        Assert.AreEqual(0, game.FlagGoods(0)[5], "the place is empty");
    }

    [TestMethod]
    public void ACarrierThatCarriesAGoodLeavesItOnTheFirstEmptyPlaceWhereNoGoodWaitsForItsRoad()
    {
        var game = WithFlag();
        game.LayGood(0, 0, Log);
        var carrier = Carrier(Stone, 1);

        game.Exchange(carrier, 0);

        Assert.AreEqual(0, carrier.Mode, "the carrier has nothing left");
        Assert.AreEqual(Stone, game.FlagGoods(0)[1]);
        Assert.AreEqual(1, game.FlagDestinations(0)[1]);
    }

    [TestMethod]
    public void ACarrierKeepsAGoodWhenAllEightPlacesAreFull()
    {
        var game = WithFlag();
        for (var place = 0; place < 8; place++)
        {
            game.LayGood(0, place, Log);
        }

        var carrier = Carrier(Stone, 1);

        game.Exchange(carrier, 0);

        Assert.AreEqual(Stone, carrier.Mode);
    }
}
