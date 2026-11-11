using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A whole game on made-up tables with the castle's hand-out on: a castle at 10,10 (its flag at 11,11), a flag at 14,11 three steps east
// with a carrier on the road between them, and a site whose door is at 13,10 and whose flag is that flag. The rules of the parts are in
// SitePassTests, RequestSearchTests, and StorageDistributionTests; these tests follow a site from its request to its goods.
[TestClass]
public sealed class ConstructionDeliveryTests
{
    private const string Layout = """
        "castle": {"x": 10, "y": 10},
        "flags": [{"x": 14, "y": 11}],
        "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}],
        """;

    private const string Carrier =
        """{"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11, "direction": 0}""";

    private static Game Started(bool carrier = true)
    {
        var read = OpenSettlers.Simulation.Layout.Read(
            LayoutTests.Text(extra: "\n" + Layout + (carrier ? $"\"settlers\": [{Carrier}]," : "")),
            "test.jsonc"
        );
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));
        var game = GameFactory.Create(ConstructionTables.Tables(), read.Layout!.BuildMap(), handsOutGoods: true);
        Assert.IsNull(read.Layout.Apply(game));

        return game;
    }

    private static void Run(Game game, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            game.Tick();
        }
    }

    private static void Place(Game game, BuildingType type)
    {
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceBuilding, 13, 10, Building: type));
        game.Tick();
    }

    private static void Drain(Game game, Good good)
    {
        while (game.CastleStock.Take(good)) { }
    }

    private static int GoodsOnFlags(Game game) =>
        Enumerable.Range(0, game.Flags.Count).Sum(flag => game.Flags[flag].Places.Count(place => !place.IsEmpty));

    [TestMethod]
    public void AForesterSiteTakesItsTwoPlanksAndNoStoneAndTheCastleLosesJustThose()
    {
        var game = Started();
        for (var plank = 0; plank < 5; plank++)
        {
            game.CastleStock.Add(Good.Plank);
            game.CastleStock.Add(Good.Stone);
        }

        var planks = game.CastleStock.Planks;
        var stones = game.CastleStock.Stones;
        Place(game, BuildingType.Forester);

        Run(game, 3000);

        var site = game.Buildings.Single(building => building.Type == BuildingType.Forester);
        // The builder takes the planks from the site's fill as they arrive, so none is on the way and none is left over.
        Assert.AreEqual(0, site.FillLow & 0x0f, "none on the way");
        Assert.AreEqual(0x00, site.FillHigh, "no stone");
        Assert.AreEqual(planks - 2, game.CastleStock.Planks);
        Assert.AreEqual(stones, game.CastleStock.Stones);
        Assert.AreEqual(
            0,
            game.Flags[site.Flag].RequestOf(RequestWord.First).Rank,
            "a site that holds what it needs asks for no more"
        );
        Assert.AreEqual(0, GoodsOnFlags(game), "nothing is left on the flags");
    }

    [TestMethod]
    public void ASiteTakenDownBeforeItIsServedGetsNoGoodAndTheStockStays()
    {
        var game = Started();
        Drain(game, Good.Plank);
        Place(game, BuildingType.Forester);
        Run(game, 80);
        Assert.IsTrue(
            game.Flags[1].RequestOf(RequestWord.First).Rank > 0xf,
            "the site asks for planks at a rank the hand-out serves"
        );

        game.Enqueue(new Command(game.TickCount, 0, CommandKind.RemoveBuilding, 13, 10));
        game.Tick();
        game.CastleStock.Add(Good.Plank);
        Run(game, 400);

        Assert.AreEqual(
            1,
            game.CastleStock.Planks,
            "the plank that entered the stock is not handed to a flag with no building"
        );
        Assert.AreEqual(0, GoodsOnFlags(game));
    }

    // A site whose flag no road joins to the castle: the request search and the hand-out travel only over roads, and the request for
    // its builder finds no way to the castle (CastleRoster.RequestWorker sets the request-failed bit), so the site gets no good and no
    // builder while the stock stays and the game runs on. ConstructionWorld's flag 17,3 is joined to no road; the site's door is 16,2.
    [TestMethod]
    public void ASiteWhoseFlagNoRoadReachesGetsNoGoodAndNoBuilderAndTheGameRunsOn()
    {
        var game = ConstructionWorld.Create(handsOutGoods: true);
        for (var good = 0; good < 5; good++)
        {
            game.CastleStock.Add(Good.Plank);
            game.CastleStock.Add(Good.Stone);
        }

        var planks = game.CastleStock.Planks;
        var stones = game.CastleStock.Stones;
        ConstructionWorld.Site(game, BuildingType.Woodcutter, 16, 2);
        var site = game.Buildings.Single(building => building.Type == BuildingType.Woodcutter);
        var failed = false;
        var builderSent = false;
        var start = game.TickCount;

        for (var tick = 0; tick < 3000; tick++)
        {
            game.Tick();
            failed |= site.RequestFailed;
            builderSent |= site.WorkerOnTheWay || site.WorkerInside;
        }

        Assert.IsTrue(failed, "the request for the builder fails");
        Assert.IsFalse(builderSent, "no builder sets out");
        Assert.AreEqual((planks, stones), (game.CastleStock.Planks, game.CastleStock.Stones), "the stock stays");
        Assert.AreEqual(0, GoodsOnFlags(game), "no good is handed out");
        Assert.AreEqual(start + 3000, game.TickCount);
    }
}
