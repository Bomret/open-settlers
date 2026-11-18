using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The pig farm's rules on made-up tables (no install), in the world of MillTests: a finished pig farm at 14,8 with its flag at 15,9
// (slot 4), which a road joins to the castle's flag at 16,6 (slot 0). The pig farmer is a settler without a job the castle makes the
// pig farmer (the request names no tool). The values the original gives are in PigFarmerTests. The generator is the game's own, so a
// test that needs a pig at once gives the pig farm its eight pigs, or makes the pigs grow at every draw.
[TestClass]
public sealed class PigFarmTests
{
    const int FarmX = 14;
    const int FarmY = 8;
    const int CastleFlag = 0;
    const int FarmFlag = 4;

    internal static (Game Game, Building Farm) World(bool handsOutGoods = false, ushort grows = 0x8000)
    {
        var tables = ConstructionTables.Tables(
            distribution: handsOutGoods ? ConstructionTables.GrainOrders() : null,
            jobData: ConstructionTables.PigData(grows)
        );
        var game = ConstructionWorld.Create(tables, handsOutGoods);
        ConstructionWorld.Do(game, CommandKind.PlaceFlag, 15, 9);
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 16, 6, [2, 2, 2, 3]);
        Assert.IsTrue(Huts.PlaceAndTick(game, FarmX, FarmY, BuildingType.PigFarm), "the pig farm stands");
        var farm = game.Buildings.Single(building => building.Type == BuildingType.PigFarm);
        Assert.AreEqual(FarmFlag, farm.Flag, "the pig farm's flag is the flag at 15,9");

        return (game, farm);
    }

    // Runs until the pig farmer waits inside, with the flag asking for grain.
    static Settler FarmerInside(Game game)
    {
        ConstructionWorld.Run(game, 1500);

        return ConstructionWorld.OfJob(game, new PigFarmer()).Single();
    }

    static Request GrainRequest(Game game) => game.Flags[FarmFlag].RequestOf(ConstructionTables.GrainClass.Word);

    // The grains the pig farm holds, on its way (the low nibble of its fill byte) and on site (the high).
    static int Held(Building farm) => (farm.FillLow >> 4) + (farm.FillLow & 0xf);

    // Runs until the condition holds and tells whether it did within the ticks.
    static bool RunUntil(Game game, System.Func<bool> condition, int ticks)
    {
        for (var tick = 0; tick < ticks && !condition(); tick++)
        {
            game.Tick();
        }

        return condition();
    }

    // Runs until the castle holds one more pig than before, and returns false where it holds none after the ticks.
    static bool RunUntilAPigReachesTheCastle(Game game, int pigs, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            game.Tick();
            if (game.CastleStock.Count(Good.Pig) > pigs)
            {
                return true;
            }
        }

        return false;
    }

    [TestMethod]
    public void ThePigFarmerWalksInAsksForGrainAndWaitsWhereNoGrainIsOnSiteAndTheFarmHoldsOnePig()
    {
        var (game, farm) = World();

        var farmer = FarmerInside(game);

        Assert.AreEqual(0, ((TendsPigs)farmer.Activity).Phase, "no grain is on site: he waits");
        Assert.IsTrue(farm.WorkerInside);
        Assert.AreEqual(1, farm.FillHigh, "his start routine sets the pigs the farm holds to one");
        Assert.AreEqual(ConstructionTables.GrainClass, GrainRequest(game).Asks, "the flag asks for grain");
        Assert.AreEqual(
            ConstructionTables.PigRankBase,
            GrainRequest(game).Rank,
            "the request starts from the player's rank: the farm holds no grain"
        );
        Assert.IsNull(
            game.Flags[FarmFlag].RequestOf(RequestWord.Second).Asks,
            "and asks for nothing in the other word"
        );
    }

    [TestMethod]
    public void WithNoGrainThePigFarmMakesNoPigAndThePigFarmerWaits()
    {
        var (game, farm) = World();
        var farmer = FarmerInside(game);
        var pigs = game.CastleStock.Count(Good.Pig);

        ConstructionWorld.Run(game, 6000);

        Assert.AreEqual(pigs, game.CastleStock.Count(Good.Pig), "no pig without grain");
        Assert.AreEqual(0, ((TendsPigs)farmer.Activity).Phase, "he still waits for grain");
        Assert.AreEqual(0, farm.FillLow);
        Assert.AreEqual(1, farm.FillHigh, "no pig grew while he waited");
    }

    [TestMethod]
    public void AGrainAtTheCastleGoesToThePigFarmAndAPigThatGrewGoesToTheCastleWhereNothingAsksForIt()
    {
        var (game, farm) = World();
        FarmerInside(game);
        var grain = game.CastleStock.Count(Good.Grain);
        var pigs = game.CastleStock.Count(Good.Pig);
        game.Goods.LayGood(CastleFlag, 0, Good.Grain);

        var reached = RunUntilAPigReachesTheCastle(game, pigs, 60000);

        Assert.IsTrue(reached, "a pig reached the castle");
        Assert.AreEqual(pigs + 1, game.CastleStock.Count(Good.Pig), "the castle's count rose by one");
        Assert.AreEqual(
            grain,
            game.CastleStock.Count(Good.Grain),
            "the grain went to the pig farm, not to the castle's stock"
        );
        Assert.AreEqual(0, farm.FillLow >> 4, "the grain was taken out of the fill");
        Assert.AreEqual(0, farm.FillLow & 0xf, "no grain stays counted on its way");
    }

    [TestMethod]
    public void AFarmThatHoldsEightPigsGivesOneAtTheEndOfTheFirstRound()
    {
        var (game, farm) = World();
        var farmer = FarmerInside(game);
        farm.FillHigh = 8;
        var pigs = game.CastleStock.Count(Good.Pig);
        game.Goods.LayGood(CastleFlag, 0, Good.Grain);
        Assert.IsTrue(RunUntil(game, () => farmer.Activity.Carries, 20000), "he carries something out");

        Assert.IsInstanceOfType<PutsDownGood>(((WaitsOutStep)farmer.Activity).Then, "he is about to lay it down");
        Assert.AreEqual(Good.Pig, ((PutsDownGood)((WaitsOutStep)farmer.Activity).Then).Good, "he carries a pig out");
        Assert.AreEqual(7, farm.FillHigh, "the farm holds one pig fewer");
        ConstructionWorld.Run(game, 3000);
        Assert.IsTrue(game.CastleStock.Count(Good.Pig) > pigs, "the pig reached the castle");
    }

    [TestMethod]
    public void AFarmThatHoldsFewerThanFourPigsGivesNoneAndNoPigGrowsWhereTheTableHoldsNoOdds()
    {
        var (game, farm) = World(grows: 0);
        var farmer = FarmerInside(game);
        var pigs = game.CastleStock.Count(Good.Pig);
        farm.FillHigh = 3;
        game.Goods.LayGood(CastleFlag, 0, Good.Grain);

        ConstructionWorld.Run(game, 30000);

        Assert.AreEqual(pigs, game.CastleStock.Count(Good.Pig), "no pig below four held");
        Assert.AreEqual(3, farm.FillHigh, "none grew where the table's draw is 0");
        Assert.IsNotInstanceOfType<StepsOutWithGood>(farmer.Activity);
    }

    [TestMethod]
    public void WhereThePigFarmHoldsEightGrainsTheFlagStopsAskingAndTheNextGrainGoesToTheCastle()
    {
        var (game, farm) = World();
        FarmerInside(game);
        for (var place = 0; place < 7; place++)
        {
            game.Goods.LayGood(1, place, Good.Grain);
        }

        // The pig farmer takes one of the seven out of the fill into his work, so the farm holds six.
        Assert.IsTrue(RunUntil(game, () => Held(farm) == 6, 6000), "six grains are held");
        ConstructionWorld.Run(game, 100);
        Assert.AreEqual(3, GrainRequest(game).Rank, "the rank 0xff shifted by six is 3: the request still asks");
        game.Goods.LayGood(1, 0, Good.Grain);
        game.Goods.LayGood(1, 1, Good.Grain);

        Assert.IsTrue(RunUntil(game, () => Held(farm) == 8, 6000), "two more are served");
        ConstructionWorld.Run(game, 100);
        Assert.AreEqual(0, GrainRequest(game).Rank, "eight grains held: the request ranks 0");
        var grain = game.CastleStock.Count(Good.Grain);
        game.Goods.LayGood(CastleFlag, 0, Good.Grain);

        ConstructionWorld.Run(game, 8000);

        Assert.AreEqual(
            grain + 1,
            game.CastleStock.Count(Good.Grain),
            "the grain that no request takes goes to the castle"
        );
    }

    [TestMethod]
    public void GrainInTheCastlesStockIsHandedOutToTheAskingPigFarm()
    {
        var (game, farm) = World(handsOutGoods: true);
        while (game.CastleStock.Take(Good.Grain)) { }

        game.CastleStock.Add(Good.Grain);
        var farmer = FarmerInside(game);

        ConstructionWorld.Run(game, 4000);

        Assert.AreEqual(0, game.CastleStock.Count(Good.Grain), "the castle handed its grain out");
        Assert.AreEqual(
            1,
            (farm.FillLow >> 4) + (farm.FillLow & 0xf) + (((TendsPigs)farmer.Activity).Phase == 0 ? 0 : 1),
            "the pig farm counts it on its way or on site, or the pig farmer took it"
        );
    }

    [TestMethod]
    public void WithNoGrainInTheStockTheCastleHandsNothingOutAndThePigFarmKeepsAsking()
    {
        var (game, farm) = World(handsOutGoods: true);
        while (game.CastleStock.Take(Good.Grain)) { }

        var farmer = FarmerInside(game);
        ConstructionWorld.Run(game, 4000);

        Assert.AreEqual(0, ((TendsPigs)farmer.Activity).Phase, "the pig farmer waits for grain");
        Assert.AreEqual(0, farm.FillLow, "nothing was counted on its way");
        Assert.AreEqual(
            ConstructionTables.PigRankBase,
            GrainRequest(game).Rank,
            "the request keeps asking at the start rank"
        );
    }

    // Lays a stone on every empty place of the pig farm's flag and returns how many it laid.
    static int FillFlag(Game game)
    {
        var laid = 0;
        for (var place = 0; place < game.Flags[FarmFlag].Places.Count; place++)
        {
            if (game.Flags[FarmFlag].Places[place].IsEmpty)
            {
                game.Goods.LayGood(FarmFlag, place, Good.Stone);
                laid++;
            }
        }

        return laid;
    }

    [TestMethod]
    public void WhereTheFlagIsFullThePigWaitsInTheDoorAndIsLaidOnceAPlaceFreesWithoutLosingAGood()
    {
        var (game, farm) = World();
        var farmer = FarmerInside(game);
        farm.FillHigh = 8;
        var stones = game.CastleStock.Count(Good.Stone);
        var pigs = game.CastleStock.Count(Good.Pig);
        game.Goods.LayGood(CastleFlag, 0, Good.Grain);
        var laid = 0;
        var tick = 0;
        while (farmer.Activity is not StepsOutWithGood && tick++ < 12000)
        {
            laid += FillFlag(game);
            game.Tick();
        }

        Assert.IsInstanceOfType<StepsOutWithGood>(farmer.Activity, "the pig is out of the pig farm");
        Assert.AreEqual(0x52, farmer.Frame, "the flag has no free place: he waits in the door");
        Assert.IsTrue(
            game.Flags[FarmFlag].Places.All(place => place.Good == Good.Stone),
            "no pig lies on the full flag"
        );

        for (var i = 0; i < 12000 && farmer.Activity is StepsOutWithGood; i++)
        {
            game.Tick();
        }

        Assert.IsNotInstanceOfType<StepsOutWithGood>(farmer.Activity, "a place freed and he stepped out");
        ConstructionWorld.Run(game, 3000);
        Assert.AreEqual(
            laid,
            game.CastleStock.Count(Good.Stone) - stones,
            "every good laid on the flag reached the castle"
        );
        Assert.IsTrue(game.CastleStock.Count(Good.Pig) > pigs, "and so did the pig");
    }
}
