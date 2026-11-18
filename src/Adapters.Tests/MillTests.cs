using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The mill's rules on made-up tables (no install), in the world of SawmillTests: a finished mill at 14,8 with its flag at 15,9 (slot 4),
// which a road joins to the castle's flag at 16,6 (slot 0). The miller is a settler without a job the castle makes the miller (the request
// names no tool). The values the original gives are in MillerTests.
[TestClass]
public sealed class MillTests
{
    const int MillX = 14;
    const int MillY = 8;
    const int CastleFlag = 0;
    const int MillFlag = 4;

    internal static (Game Game, Building Mill) World(
        bool handsOutGoods = false,
        bool grainOrder = false,
        byte[]? kinds = null
    )
    {
        var tables = ConstructionTables.Tables(
            distribution: grainOrder ? ConstructionTables.GrainOrders() : null,
            goodKinds: kinds
        );
        var game = ConstructionWorld.Create(tables, handsOutGoods);
        ConstructionWorld.Do(game, CommandKind.PlaceFlag, 15, 9);
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 16, 6, [2, 2, 2, 3]);
        Assert.IsTrue(Huts.PlaceAndTick(game, MillX, MillY, BuildingType.Mill), "the mill stands");
        var mill = game.Buildings.Single(building => building.Type == BuildingType.Mill);
        Assert.AreEqual(MillFlag, mill.Flag, "the mill's flag is the flag at 15,9");

        return (game, mill);
    }

    // Runs until the miller waits inside, with the flag asking for grain.
    internal static Settler MillerInside(Game game)
    {
        ConstructionWorld.Run(game, 1500);

        return ConstructionWorld.OfJob(game, new Miller()).Single();
    }

    static Request GrainRequest(Game game) => game.Flags[MillFlag].RequestOf(ConstructionTables.GrainClass.Word);

    [TestMethod]
    public void TheMillerWalksInAsksForGrainAndWaitsWhereNoGrainIsOnSite()
    {
        var (game, mill) = World();

        var miller = MillerInside(game);

        Assert.AreEqual(0, ((Grinds)miller.Activity).Phase, "no grain is on site: he waits");
        Assert.IsTrue(mill.WorkerInside);
        Assert.AreEqual(ConstructionTables.GrainClass, GrainRequest(game).Asks, "the flag asks for grain");
        Assert.AreEqual(
            ConstructionTables.MillRankBase,
            GrainRequest(game).Rank,
            "the request starts from the player's rank: the mill holds no grain"
        );
        Assert.IsNull(
            game.Flags[MillFlag].RequestOf(RequestWord.Second).Asks,
            "and asks for nothing in the other word"
        );
    }

    [TestMethod]
    public void AGrainAtTheCastleGoesToTheMillAndIsGroundIntoOneFlourThatReachesTheCastle()
    {
        var (game, mill) = World();
        MillerInside(game);
        var grain = game.CastleStock.Count(Good.Grain);
        var flour = game.CastleStock.Count(Good.Flour);
        game.LayGood(CastleFlag, 0, Good.Grain);

        ConstructionWorld.Run(game, 12000);

        Assert.AreEqual(
            grain,
            game.CastleStock.Count(Good.Grain),
            "the grain went to the mill, not to the castle's stock"
        );
        Assert.AreEqual(flour + 1, game.CastleStock.Count(Good.Flour), "one grain made one flour");
        Assert.AreEqual(0, mill.FillLow, "no grain stays counted, on site or on the way");
        Assert.IsFalse(mill.AtWork, "the mill is no longer at work");
    }

    [TestMethod]
    public void WhereTheMillHoldsSixGrainsTheNextGrainGoesToTheCastle()
    {
        var (game, mill) = World();
        MillerInside(game);
        for (var place = 0; place < 7; place++)
        {
            game.LayGood(1, place, Good.Grain);
        }

        ConstructionWorld.Run(game, 80);

        Assert.AreEqual(
            6,
            (mill.FillLow >> 4) + (mill.FillLow & 0xf),
            "six are served to the mill, whose rank 0x3f is then shifted to 0"
        );
        Assert.AreEqual(0, GrainRequest(game).Rank, "six grains on the way fill the store: the request ranks 0");
        var grain = game.CastleStock.Count(Good.Grain);

        ConstructionWorld.Run(game, 8000);

        Assert.AreEqual(
            grain + 1,
            game.CastleStock.Count(Good.Grain),
            "the grain that no request takes goes to the castle"
        );
    }

    [TestMethod]
    public void AGrainThatLosesItsRoadIsCountedBackInTheByteItWasCountedIn()
    {
        // The made-up kind table sends the grain to the high byte where its request word counts the low one: the byte a grain is counted
        // in on its way and the byte it is counted back from agree whichever table says it.
        var kinds = new byte[TestCatalog.Slots];
        kinds[TestCatalog.CodeOf(Good.Grain)] = 1;
        var (game, mill) = World(kinds: kinds);
        MillerInside(game);
        game.LayGood(1, 0, Good.Grain);
        for (var tick = 0; tick < 100 && mill.FillLow + mill.FillHigh == 0; tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(1, mill.FillLow + mill.FillHigh, "the grain on its way is counted once");

        ConstructionWorld.Do(game, CommandKind.RemoveFlag, 18, 6);

        Assert.AreEqual(0, mill.FillLow, "no grain stays counted as on its way in the low byte");
        Assert.AreEqual(0, mill.FillHigh, "and none was counted back out of the high byte");
    }

    [TestMethod]
    public void WithNoGrainTheMillMakesNoFlourAndTheMillerWaits()
    {
        var (game, mill) = World();
        var miller = MillerInside(game);
        var flour = game.CastleStock.Count(Good.Flour);

        ConstructionWorld.Run(game, 4000);

        Assert.AreEqual(flour, game.CastleStock.Count(Good.Flour), "no flour without grain");
        Assert.AreEqual(0, ((Grinds)miller.Activity).Phase, "he still waits for grain");
        Assert.IsFalse(mill.AtWork);
        Assert.AreEqual(0, mill.FillLow);
    }

    [TestMethod]
    public void GrainInTheCastlesStockIsHandedOutToTheAskingMillAndGroundIntoFlourThatReachesTheCastle()
    {
        var (game, mill) = World(handsOutGoods: true, grainOrder: true);
        while (game.CastleStock.Take(Good.Grain)) { }

        game.CastleStock.Add(Good.Grain);
        var flour = game.CastleStock.Count(Good.Flour);
        MillerInside(game);

        ConstructionWorld.Run(game, 14000);

        Assert.AreEqual(0, game.CastleStock.Count(Good.Grain), "the castle handed its grain out");
        Assert.AreEqual(
            flour + 1,
            game.CastleStock.Count(Good.Flour),
            "the mill made one flour of it and the castle holds it"
        );
        Assert.AreEqual(0, mill.FillLow, "no grain stays counted on its way or on site");
    }

    [TestMethod]
    public void WithNoGrainInTheStockTheCastleHandsNothingOutAndTheGameKeepsRunning()
    {
        var (game, mill) = World(handsOutGoods: true, grainOrder: true);
        while (game.CastleStock.Take(Good.Grain)) { }

        var miller = MillerInside(game);
        ConstructionWorld.Run(game, 4000);

        Assert.AreEqual(0, ((Grinds)miller.Activity).Phase, "the miller waits for grain");
        Assert.AreEqual(0, mill.FillLow, "nothing was counted on its way");
        Assert.AreEqual(
            ConstructionTables.MillRankBase,
            GrainRequest(game).Rank,
            "the request keeps asking at the start rank"
        );
    }

    // Lays a stone on every empty place of the mill's flag and returns how many it laid.
    static int FillFlag(Game game)
    {
        var laid = 0;
        for (var place = 0; place < game.Flags[MillFlag].Places.Count; place++)
        {
            if (game.Flags[MillFlag].Places[place].IsEmpty)
            {
                game.LayGood(MillFlag, place, Good.Stone);
                laid++;
            }
        }

        return laid;
    }

    [TestMethod]
    public void WhereTheFlagIsFullTheFlourWaitsInTheDoorAndIsLaidOnceAPlaceFreesWithoutLosingAGood()
    {
        var (game, _) = World();
        var miller = MillerInside(game);
        var stones = game.CastleStock.Count(Good.Stone);
        var flour = game.CastleStock.Count(Good.Flour);
        game.LayGood(CastleFlag, 0, Good.Grain);
        var laid = 0;
        var tick = 0;
        while (miller.Activity is not StepsOutWithGood && tick++ < 12000)
        {
            laid += FillFlag(game);
            game.Tick();
        }

        Assert.IsInstanceOfType<StepsOutWithGood>(miller.Activity, "the flour is out of the mill");
        Assert.AreEqual(0x52, miller.Frame, "the flag has no free place: he waits in the door");
        Assert.IsTrue(
            game.Flags[MillFlag].Places.All(place => place.Good == Good.Stone),
            "no flour lies on the full flag"
        );

        for (var i = 0; i < 12000 && miller.Activity is StepsOutWithGood; i++)
        {
            game.Tick();
        }

        Assert.IsNotInstanceOfType<StepsOutWithGood>(miller.Activity, "a place freed and he stepped out");
        ConstructionWorld.Run(game, 3000);
        Assert.AreEqual(
            laid,
            game.CastleStock.Count(Good.Stone) - stones,
            "every good laid on the flag reached the castle"
        );
        Assert.AreEqual(flour + 1, game.CastleStock.Count(Good.Flour), "and so did the flour");
    }
}
