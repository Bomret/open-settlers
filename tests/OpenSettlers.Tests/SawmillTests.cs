using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The sawmill's rules on made-up tables (no install), in the world of DiggerTests: a finished sawmill at 14,8 with its flag at 15,9 (slot 4), which
// a road joins to the castle's flag at 16,6 (slot 0), and the flags 18,6 (slot 1), 19,4, and 17,3 with their roads. The sawmiller is the castle's settler
// of the job 6. The values the original gives are in SawmillerTests.
[TestClass]
public sealed class SawmillTests
{
    private const int SawmillX = 14;
    private const int SawmillY = 8;
    private const int CastleFlag = 0;
    private const int SawmillFlag = 4;

    private static (Game Game, Building Sawmill) World(bool handsOutGoods = false)
    {
        var game = ConstructionWorld.Create(ConstructionTables.Tables(), handsOutGoods);
        ConstructionWorld.Do(game, CommandKind.PlaceFlag, 15, 9);
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 16, 6, [2, 2, 2, 3]);
        Assert.IsTrue(Huts.PlaceAndTick(game, SawmillX, SawmillY, BuildingType.Sawmill), "the sawmill stands");
        var sawmill = game.Buildings.Single(building => building.Type == BuildingType.Sawmill);
        Assert.AreEqual(SawmillFlag, sawmill.Flag, "the sawmill's flag is the flag at 15,9");

        return (game, sawmill);
    }

    // Runs until the sawmiller waits inside, with the flag asking for logs.
    private static Settler SawmillerInside(Game game)
    {
        ConstructionWorld.Run(game, 1500);

        return ConstructionWorld.OfJob(game, Job.Sawmiller).Single();
    }

    private static Request LogRequest(Game game) => game.Flags[SawmillFlag].RequestOf(ConstructionTables.LogClass.Word);

    [TestMethod]
    public void TheSawmillerWalksInAsksForLogsAndWaitsWhereNoLogIsOnSite()
    {
        var (game, sawmill) = World();

        var sawmiller = SawmillerInside(game);

        Assert.IsInstanceOfType<Saws>(sawmiller.Activity);
        Assert.IsFalse(((Saws)sawmiller.Activity).Sawing, "no log is on site: he waits");
        Assert.IsTrue(sawmill.WorkerInside);
        Assert.AreEqual(ConstructionTables.LogClass, LogRequest(game).Asks, "the flag asks for logs");
        Assert.AreEqual(0xff, LogRequest(game).Rank, "the first request has the full rank: the sawmill holds no log");
        Assert.IsNull(game.Flags[SawmillFlag].RequestOf(RequestWord.First).Asks, "and for no plank");
    }

    [TestMethod]
    public void ALogAtTheCastleGoesToTheSawmillAndIsSawnIntoOnePlankThatReachesTheCastle()
    {
        var (game, _) = World();
        SawmillerInside(game);
        var logs = game.CastleStock.Logs;
        var planks = game.CastleStock.Planks;
        game.Goods.LayGood(CastleFlag, 0, Good.Log.Code);

        ConstructionWorld.Run(game, 12000);

        Assert.AreEqual(logs, game.CastleStock.Logs, "the log went to the sawmill, not to the castle's stock");
        Assert.AreEqual(planks + 1, game.CastleStock.Planks, "one log made one plank");
    }

    [TestMethod]
    public void WhereTheSawmillHoldsEightLogsTheNextLogGoesToTheCastle()
    {
        var (game, sawmill) = World();
        SawmillerInside(game);
        for (var place = 0; place < 8; place++)
        {
            game.Goods.LayGood(1, place, Good.Log.Code);
        }

        ConstructionWorld.Run(game, 80);

        Assert.AreEqual(8, (sawmill.FillHigh >> 4) + (sawmill.FillHigh & 0xf), "all eight are served to the sawmill");
        Assert.AreEqual(0, LogRequest(game).Rank, "eight logs on the way fill the store: the request ranks 0");
        var logs = game.CastleStock.Logs;
        game.Goods.LayGood(SawmillFlag, 5, Good.Log.Code);

        ConstructionWorld.Run(game, 8000);

        Assert.AreEqual(logs + 1, game.CastleStock.Logs, "the log that no request takes goes to the castle");
    }

    [TestMethod]
    public void APlankFromTheSawmillGoesToASiteThatAsksForPlanksInsteadOfTheCastle()
    {
        var (game, _) = World();
        SawmillerInside(game);
        ConstructionWorld.Site(game, BuildingType.Forester, 17, 5);
        var site = game.Buildings.Single(building => building.Type == BuildingType.Forester);
        var planks = game.CastleStock.Planks;
        game.Goods.LayGood(CastleFlag, 0, Good.Log.Code);

        ConstructionWorld.Run(game, 12000);

        var held = (site.FillLow >> 4) + (site.FillLow & 0xf) + (2 - (site.Site?.Planks ?? 0));
        Assert.IsGreaterThanOrEqualTo(1, held, "the site got the plank");
        Assert.AreEqual(planks, game.CastleStock.Planks, "the castle got none");
    }

    [TestMethod]
    public void PlanksSawnFromTwoLogsBuildAForesterSiteWhileTheCastleHoldsNoPlank()
    {
        var (game, _) = World(handsOutGoods: true);
        while (game.CastleStock.Take(Good.Plank)) { }

        SawmillerInside(game);
        ConstructionWorld.Site(game, BuildingType.Forester, 17, 5);
        var site = game.Buildings.Single(building => building.Type == BuildingType.Forester);
        game.CastleStock.Add(ConstructionTables.BuilderTool);
        game.Goods.LayGood(CastleFlag, 0, Good.Log.Code);
        game.Goods.LayGood(CastleFlag, 1, Good.Log.Code);

        for (var tick = 0; tick < 60000 && site.Constructing; tick++)
        {
            game.Tick();
        }

        Assert.IsFalse(site.Constructing, "the forester's site is built from the planks the sawmill made");
        Assert.AreEqual(0, game.CastleStock.Planks, "none came from, or went to, the castle's stock");
    }
}
