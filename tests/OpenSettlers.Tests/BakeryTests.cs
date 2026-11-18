using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The bakery's rules on made-up tables (no install), in the world of MillTests: a finished bakery at 14,8 with its flag at 15,9 (slot 4),
// which a road joins to the castle's flag at 16,6 (slot 0). The baker is a settler without a job the castle makes the baker (the request
// names no tool). The values the original gives are in BakerWalkTests.
[TestClass]
public sealed class BakeryTests
{
    const int BakeryX = 14;
    const int BakeryY = 8;
    const int CastleFlag = 0;
    const int BakeryFlag = 4;

    // The made-up rank the original's handler starts the flour request from: 0xff, a constant of the handler.
    const byte RankBase = 0xff;

    internal static (Game Game, Building Bakery) World(
        bool handsOutGoods = false,
        bool flourOrder = false,
        byte[]? kinds = null
    )
    {
        var tables = ConstructionTables.Tables(
            distribution: flourOrder ? ConstructionTables.FlourOrders() : null,
            goodKinds: kinds
        );
        var game = ConstructionWorld.Create(tables, handsOutGoods);
        ConstructionWorld.Do(game, CommandKind.PlaceFlag, 15, 9);
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 16, 6, [2, 2, 2, 3]);
        Assert.IsTrue(Huts.PlaceAndTick(game, BakeryX, BakeryY, BuildingType.Baker), "the bakery stands");
        var bakery = game.Buildings.Single(building => building.Type == BuildingType.Baker);
        Assert.AreEqual(BakeryFlag, bakery.Flag, "the bakery's flag is the flag at 15,9");

        return (game, bakery);
    }

    // Runs until the baker waits inside, with the flag asking for flour.
    internal static Settler BakerInside(Game game)
    {
        ConstructionWorld.Run(game, 1500);

        return ConstructionWorld.OfJob(game, new Baker()).Single();
    }

    static Request FlourRequest(Game game) => game.Flags[BakeryFlag].RequestOf(ConstructionTables.FlourClass.Word);

    [TestMethod]
    public void TheBakerWalksInAsksForFlourAndWaitsWhereNoFlourIsOnSite()
    {
        var (game, bakery) = World();

        var baker = BakerInside(game);

        Assert.AreEqual(0, ((Bakes)baker.Activity).Phase, "no flour is on site: he waits");
        Assert.IsTrue(bakery.WorkerInside);
        Assert.AreEqual(ConstructionTables.FlourClass, FlourRequest(game).Asks, "the flag asks for flour");
        Assert.AreEqual(RankBase, FlourRequest(game).Rank, "the request starts from 0xff: the bakery holds no flour");
        Assert.IsNull(
            game.Flags[BakeryFlag].RequestOf(RequestWord.Second).Asks,
            "and asks for nothing in the other word"
        );
    }

    [TestMethod]
    public void AFlourAtTheCastleGoesToTheBakeryAndIsBakedIntoOneBreadThatReachesTheCastle()
    {
        var (game, bakery) = World();
        BakerInside(game);
        var flour = game.CastleStock.Count(Good.Flour);
        var bread = game.CastleStock.Count(Good.Bread);
        game.Goods.LayGood(CastleFlag, 0, Good.Flour);

        ConstructionWorld.Run(game, 12000);

        Assert.AreEqual(
            flour,
            game.CastleStock.Count(Good.Flour),
            "the flour went to the bakery, not to the castle's stock"
        );
        Assert.AreEqual(bread + 1, game.CastleStock.Count(Good.Bread), "one flour made one bread");
        Assert.AreEqual(0, bakery.FillLow, "no flour stays counted, on site or on the way");
        Assert.IsFalse(bakery.AtWork, "the bakery is no longer at work");
    }

    [TestMethod]
    public void TheBakerBakesInTwoStretchesTheSecondOneWithTheNodeFreeAndTheBuildingMarkedAtWork()
    {
        var (game, bakery) = World();
        var baker = BakerInside(game);
        game.Goods.LayGood(CastleFlag, 0, Good.Flour);
        var phases = new System.Collections.Generic.List<int>();
        var marked = new System.Collections.Generic.List<bool>();
        var carriedOut = false;
        for (var tick = 0; tick < 12000 && phases.Count < 4; tick++)
        {
            game.Tick();
            if (baker.Activity is Bakes bakes && (phases.Count == 0 || phases[^1] != bakes.Phase))
            {
                phases.Add(bakes.Phase);
                marked.Add(bakery.AtWork);
            }

            carriedOut |=
                baker.Activity is WaitsOutStep { Then: PutsDownGood { Good: var good } } && good == Good.Bread;
        }

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 0 }, phases, "waiting, baking, pausing, waiting for flour again");
        CollectionAssert.AreEqual(
            new[] { false, false, true, false },
            marked,
            "the building is marked as working in the pause only"
        );
        Assert.IsTrue(carriedOut, "he carried the bread out between the pause and the wait");
    }

    [TestMethod]
    public void WhereTheBakeryHoldsSevenFloursTheEighthIsTheLastItTakesAndTheNextGoesToTheCastle()
    {
        var (game, bakery) = World();
        BakerInside(game);
        for (var place = 0; place < 8; place++)
        {
            game.Goods.LayGood(1, place, Good.Flour);
        }

        ConstructionWorld.Run(game, 80);

        Assert.AreEqual(
            8,
            (bakery.FillLow >> 4) + (bakery.FillLow & 0xf),
            "eight are served to the bakery, whose rank 0xff is then shifted to 0"
        );
        Assert.AreEqual(0, FlourRequest(game).Rank, "eight flours on the way fill the store: the request ranks 0");
        game.Goods.LayGood(CastleFlag, 0, Good.Flour);
        var flour = game.CastleStock.Count(Good.Flour);

        ConstructionWorld.Run(game, 8000);

        Assert.AreEqual(
            flour + 1,
            game.CastleStock.Count(Good.Flour),
            "the flour that no request takes goes to the castle"
        );
    }

    [TestMethod]
    public void AFlourThatLosesItsRoadIsCountedBackInTheByteItWasCountedIn()
    {
        // The made-up kind table sends the flour to the high byte where its request word counts the low one: the byte a flour is counted
        // in on its way and the byte it is counted back from agree whichever table says it.
        var kinds = new byte[TestCatalog.Slots];
        kinds[TestCatalog.CodeOf(Good.Flour)] = 1;
        var (game, bakery) = World(kinds: kinds);
        BakerInside(game);
        game.Goods.LayGood(1, 0, Good.Flour);
        for (var tick = 0; tick < 100 && bakery.FillLow + bakery.FillHigh == 0; tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(1, bakery.FillLow + bakery.FillHigh, "the flour on its way is counted once");

        ConstructionWorld.Do(game, CommandKind.RemoveFlag, 18, 6);

        Assert.AreEqual(0, bakery.FillLow, "no flour stays counted as on its way in the low byte");
        Assert.AreEqual(0, bakery.FillHigh, "and none was counted back out of the high byte");
    }

    [TestMethod]
    public void WithNoFlourTheBakeryMakesNoBreadAndTheBakerWaits()
    {
        var (game, bakery) = World();
        var baker = BakerInside(game);
        var bread = game.CastleStock.Count(Good.Bread);

        ConstructionWorld.Run(game, 4000);

        Assert.AreEqual(bread, game.CastleStock.Count(Good.Bread), "no bread without flour");
        Assert.AreEqual(0, ((Bakes)baker.Activity).Phase, "he still waits for flour");
        Assert.IsFalse(bakery.AtWork);
        Assert.AreEqual(0, bakery.FillLow);
    }

    [TestMethod]
    public void FlourInTheCastlesStockIsHandedOutToTheAskingBakeryAndBakedIntoBreadThatReachesTheCastle()
    {
        var (game, bakery) = World(handsOutGoods: true, flourOrder: true);
        while (game.CastleStock.Take(Good.Flour)) { }

        game.CastleStock.Add(Good.Flour);
        var bread = game.CastleStock.Count(Good.Bread);
        BakerInside(game);

        ConstructionWorld.Run(game, 14000);

        Assert.AreEqual(0, game.CastleStock.Count(Good.Flour), "the castle handed its flour out");
        Assert.AreEqual(
            bread + 1,
            game.CastleStock.Count(Good.Bread),
            "the bakery made one bread of it and the castle holds it"
        );
        Assert.AreEqual(0, bakery.FillLow, "no flour stays counted on its way or on site");
    }

    [TestMethod]
    public void WithNoFlourInTheStockTheCastleHandsNothingOutAndTheGameKeepsRunning()
    {
        var (game, bakery) = World(handsOutGoods: true, flourOrder: true);
        while (game.CastleStock.Take(Good.Flour)) { }

        var baker = BakerInside(game);
        ConstructionWorld.Run(game, 4000);

        Assert.AreEqual(0, ((Bakes)baker.Activity).Phase, "the baker waits for flour");
        Assert.AreEqual(0, bakery.FillLow, "nothing was counted on its way");
        Assert.AreEqual(RankBase, FlourRequest(game).Rank, "the request keeps asking at the start rank");
    }

    // Lays a stone on every empty place of the bakery's flag and returns how many it laid.
    static int FillFlag(Game game)
    {
        var laid = 0;
        for (var place = 0; place < game.Flags[BakeryFlag].Places.Count; place++)
        {
            if (game.Flags[BakeryFlag].Places[place].IsEmpty)
            {
                game.Goods.LayGood(BakeryFlag, place, Good.Stone);
                laid++;
            }
        }

        return laid;
    }

    [TestMethod]
    public void WhereTheFlagIsFullTheBreadWaitsInTheDoorAndIsLaidOnceAPlaceFreesWithoutLosingAGood()
    {
        var (game, _) = World();
        var baker = BakerInside(game);
        var stones = game.CastleStock.Count(Good.Stone);
        var bread = game.CastleStock.Count(Good.Bread);
        game.Goods.LayGood(CastleFlag, 0, Good.Flour);
        var laid = 0;
        var tick = 0;
        while (baker.Activity is not StepsOutWithGood && tick++ < 12000)
        {
            laid += FillFlag(game);
            game.Tick();
        }

        Assert.IsInstanceOfType<StepsOutWithGood>(baker.Activity, "the bread is out of the bakery");
        Assert.AreEqual(0x52, baker.Frame, "the flag has no free place: he waits in the door");
        Assert.IsTrue(
            game.Flags[BakeryFlag].Places.All(place => place.Good == Good.Stone),
            "no bread lies on the full flag"
        );

        for (var i = 0; i < 12000 && baker.Activity is StepsOutWithGood; i++)
        {
            game.Tick();
        }

        Assert.IsNotInstanceOfType<StepsOutWithGood>(baker.Activity, "a place freed and he stepped out");
        ConstructionWorld.Run(game, 3000);
        Assert.AreEqual(
            laid,
            game.CastleStock.Count(Good.Stone) - stones,
            "every good laid on the flag reached the castle"
        );
        Assert.AreEqual(bread + 1, game.CastleStock.Count(Good.Bread), "and so did the bread");
    }
}
