using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The butcher's rules on made-up tables (no install), in the world of MillTests: a finished butcher's shop at 14,8 with its flag at 15,9
// (slot 4), which a road joins to the castle's flag at 16,6 (slot 0). The butcher is a settler without a job the castle makes the butcher
// (the made-up request names no tool). The values the original gives are in ButcherTests.
[TestClass]
public sealed class ButcherShopTests
{
    const int ShopX = 14;
    const int ShopY = 8;
    const int CastleFlag = 0;
    const int ShopFlag = 4;

    internal static (Game Game, Building Shop) World(bool handsOutGoods = false, bool pigOrder = false)
    {
        var tables = ConstructionTables.Tables(distribution: pigOrder ? ConstructionTables.PigOrders() : null);
        var game = ConstructionWorld.Create(tables, handsOutGoods);
        ConstructionWorld.Do(game, CommandKind.PlaceFlag, 15, 9);
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 16, 6, [2, 2, 2, 3]);
        Assert.IsTrue(Huts.PlaceAndTick(game, ShopX, ShopY, BuildingType.Butcher), "the butcher's shop stands");
        var shop = game.Buildings.Single(building => building.Type == BuildingType.Butcher);
        Assert.AreEqual(ShopFlag, shop.Flag, "the shop's flag is the flag at 15,9");

        return (game, shop);
    }

    // Runs until the butcher waits inside, with the flag asking for pigs.
    internal static Settler ButcherInside(Game game)
    {
        ConstructionWorld.Run(game, 1500);

        return ConstructionWorld.OfJob(game, new Butcher()).Single();
    }

    static Request PigRequest(Game game) => game.Flags[ShopFlag].RequestOf(ConstructionTables.PigClass.Word);

    [TestMethod]
    public void TheButcherWalksInAsksForPigsAtTheFullRankAndWaitsWhereNoPigIsOnSite()
    {
        var (game, shop) = World();

        var butcher = ButcherInside(game);

        Assert.IsFalse(((Butchers)butcher.Activity).Working, "no pig is on site: he waits");
        Assert.IsTrue(shop.WorkerInside);
        Assert.AreEqual(ConstructionTables.PigClass, PigRequest(game).Asks, "the flag asks for pigs");
        Assert.AreEqual(0xff, PigRequest(game).Rank, "the shop holds no pig: the rank is 0xff");
        Assert.IsNull(game.Flags[ShopFlag].RequestOf(RequestWord.Second).Asks, "and the other word asks for nothing");
    }

    [TestMethod]
    public void APigAtTheCastleGoesToTheShopAndOnePigMakesOneMeatThatReachesTheCastle()
    {
        var (game, shop) = World();
        ButcherInside(game);
        var pigs = game.CastleStock.Count(Good.Pig);
        var meat = game.CastleStock.Count(Good.Meat);
        game.LayGood(CastleFlag, 0, Good.Pig);

        ConstructionWorld.Run(game, 12000);

        Assert.AreEqual(pigs, game.CastleStock.Count(Good.Pig), "the pig went to the shop, not to the castle's stock");
        Assert.AreEqual(meat + 1, game.CastleStock.Count(Good.Meat), "one pig made one meat");
        Assert.AreEqual(0, shop.FillLow, "no pig stays counted, on site or on the way");
    }

    [TestMethod]
    public void TheRankHalvesWithEachPigTheShopHoldsAndTheNinthPigGoesToTheCastle()
    {
        var (game, shop) = World();
        ButcherInside(game);
        for (var place = 0; place < 8; place++)
        {
            game.LayGood(1, place, Good.Pig);
        }

        game.LayGood(2, 0, Good.Pig);

        ConstructionWorld.Run(game, 100);

        Assert.AreEqual(8, (shop.FillLow >> 4) + (shop.FillLow & 0xf), "eight are served to the shop");
        Assert.AreEqual(0, PigRequest(game).Rank, "eight pigs held make the request rank 0: the flag stops asking");
        var pigs = game.CastleStock.Count(Good.Pig);

        ConstructionWorld.Run(game, 9000);

        Assert.AreEqual(pigs + 1, game.CastleStock.Count(Good.Pig), "the pig no request takes goes to the castle");
    }

    [TestMethod]
    public void WithNoPigTheShopMakesNoMeatAndTheButcherWaits()
    {
        var (game, shop) = World();
        var butcher = ButcherInside(game);
        var meat = game.CastleStock.Count(Good.Meat);

        ConstructionWorld.Run(game, 4000);

        Assert.AreEqual(meat, game.CastleStock.Count(Good.Meat), "no meat without a pig");
        Assert.IsFalse(((Butchers)butcher.Activity).Working, "he still waits for a pig");
        Assert.AreEqual(0, shop.FillLow);
    }

    [TestMethod]
    public void APigInTheCastlesStockIsHandedOutToTheAskingShopAndMadeIntoMeatThatReachesTheCastle()
    {
        var (game, shop) = World(handsOutGoods: true, pigOrder: true);
        while (game.CastleStock.Take(Good.Pig)) { }

        game.CastleStock.Add(Good.Pig);
        var meat = game.CastleStock.Count(Good.Meat);
        ButcherInside(game);

        ConstructionWorld.Run(game, 14000);

        Assert.AreEqual(0, game.CastleStock.Count(Good.Pig), "the castle handed its pig out");
        Assert.AreEqual(meat + 1, game.CastleStock.Count(Good.Meat), "the shop made one meat of it");
        Assert.AreEqual(0, shop.FillLow, "no pig stays counted on its way or on site");
    }

    [TestMethod]
    public void WhereTheFlagIsFullTheMeatWaitsInTheDoorAndIsLaidOnceAPlaceFreesWithoutLosingAGood()
    {
        var (game, _) = World();
        var butcher = ButcherInside(game);
        var stones = game.CastleStock.Count(Good.Stone);
        var meat = game.CastleStock.Count(Good.Meat);
        game.LayGood(CastleFlag, 0, Good.Pig);
        var laid = 0;
        var tick = 0;
        while (butcher.Activity is not StepsOutWithGood && tick++ < 12000)
        {
            for (var place = 0; place < game.Flags[ShopFlag].Places.Count; place++)
            {
                if (game.Flags[ShopFlag].Places[place].IsEmpty)
                {
                    game.LayGood(ShopFlag, place, Good.Stone);
                    laid++;
                }
            }

            game.Tick();
        }

        Assert.IsInstanceOfType<StepsOutWithGood>(butcher.Activity, "the meat is out of the shop");
        Assert.AreEqual(0x52, butcher.Frame, "the flag has no free place: he waits in the door");

        for (var i = 0; i < 12000 && butcher.Activity is StepsOutWithGood; i++)
        {
            game.Tick();
        }

        Assert.IsNotInstanceOfType<StepsOutWithGood>(butcher.Activity, "a place freed and he stepped out");
        ConstructionWorld.Run(game, 3000);
        Assert.AreEqual(
            laid,
            game.CastleStock.Count(Good.Stone) - stones,
            "every good laid on the flag reached the castle"
        );
        Assert.AreEqual(meat + 1, game.CastleStock.Count(Good.Meat), "and so did the meat");
    }
}
