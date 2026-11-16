using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A food building from its site to its worker, on made-up tables with the castle's hand-out on: the site at 14,8 stands on uneven ground
// when large, receives its planks and stones over the road to its flag at 15,9, is levelled by the digger when large, is built, and then
// asks for its worker with the job and the tool its handler names (docs/rules/food-buildings.md). The numbers of the needs, the order, the
// job, and the tool are made up; the program's own are in ProgramTablesTests.
[TestClass]
public sealed class FoodBuildingTests
{
    const int SiteX = 14;
    const int SiteY = 8;
    const int Planks = 2;
    const int Stones = 1;
    static readonly Good FoodTool = new() { Code = 21 };

    // The made-up request of every food type: a job of its own between 11 and 20 (the castle holds no settler of those), with the tool
    // for the types whose number is even.
    static WorkerRequest Request(BuildingType type) =>
        new() { Job = Job.Of(11 + type.Code % 10), Tool = type.Code % 2 == 0 ? FoodTool : Good.None };

    static (Game Game, Building Site) World(BuildingType type, bool large)
    {
        var needs = new byte[2 * RuleTables.BuildingTypeCount];
        needs[2 * type.Code] = Planks;
        needs[2 * type.Code + 1] = Stones;
        var order = new ushort[RuleTables.BuildingTypeCount];
        order[type.Code] = 0b10;
        var sizes = ConstructionTables.Sizes();
        sizes[type.Code] = (byte)(large ? 2 : 1);
        var workers = ConstructionTables.Workers();
        workers[type.Code] = Request(type);
        var steps = ConstructionTables.BlowSteps();
        steps[2 * type.Code] = 0x1000;
        steps[2 * type.Code + 1] = 0x1000;
        var game = ConstructionWorld.Create(
            ConstructionTables.Tables(
                needs: needs,
                materialOrder: order,
                sizes: sizes,
                workers: workers,
                blowSteps: steps
            ),
            handsOutGoods: true
        );
        game.CastleStock.Add(ConstructionTables.DiggerTool);
        while (game.CastleStock.Take(FoodTool)) { }

        for (var count = 0; count < 5; count++)
        {
            game.CastleStock.Add(Good.Plank);
            game.CastleStock.Add(Good.Stone);
        }

        ConstructionWorld.Do(game, CommandKind.PlaceFlag, 15, 9);
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 16, 6, [2, 2, 2, 3]);
        ConstructionWorld.Site(game, type, SiteX, SiteY);
        var site = game.Buildings[1];
        Assert.IsTrue(site.Constructing, "the site stands");
        if (large)
        {
            // The ground is uneven: the node lies two below the site's height, so the digger has work.
            game.Map.At(SiteX, SiteY).Height = site.Site!.TargetHeight - 2;
        }

        return (game, site);
    }

    static readonly (BuildingType Type, bool Large)[] Types =
    [
        (BuildingType.Fisher, false),
        (BuildingType.Farm, true),
        (BuildingType.Butcher, true),
        (BuildingType.PigFarm, true),
        (BuildingType.Mill, false),
        (BuildingType.Baker, true),
    ];

    [TestMethod]
    public void EachFoodSiteIsLevelledWhereLargeFilledWithItsPlanksAndStonesAndBuilt()
    {
        foreach (var (type, large) in Types)
        {
            var (game, site) = World(type, large);
            var planks = game.CastleStock.Planks;
            var stones = game.CastleStock.Stones;
            var started = site.Progress;

            for (var tick = 0; tick < 60000 && site.Constructing; tick++)
            {
                game.Tick();
                if (large && site.Constructing && site.Progress == 0)
                {
                    Assert.AreEqual(
                        planks,
                        game.CastleStock.Planks,
                        $"type {type}: a large site not yet level asks for no plank"
                    );
                }
            }

            Assert.AreEqual(large ? 0 : 1, started, $"type {type}: progress at the start");
            Assert.IsFalse(site.Constructing, $"type {type} is built");
            Assert.AreEqual(planks - Planks, game.CastleStock.Planks, $"type {type}: the planks that went out");
            Assert.AreEqual(stones - Stones, game.CastleStock.Stones, $"type {type}: the stones that went out");
        }
    }

    [TestMethod]
    public void AFinishedFoodBuildingAsksForItsJobAndToolAndAJoblessSettlerWithTheToolBecomesItsWorker()
    {
        foreach (var (type, large) in Types)
        {
            var (game, site) = World(type, large);
            game.CastleStock.Add(FoodTool);
            var tools = game.CastleStock.Count(FoodTool);
            for (var tick = 0; tick < 60000 && site.Constructing; tick++)
            {
                game.Tick();
            }

            for (var tick = 0; tick < 3000 && !site.WorkerOnTheWay; tick++)
            {
                game.Tick();
            }

            var asked = Request(type);
            Assert.IsTrue(site.WorkerOnTheWay || site.WorkerInside, $"type {type}: the worker is on the way");
            Assert.IsTrue(
                game.Settlers.Any(settler => settler.Job == asked.Job),
                $"type {type}: a settler took the job {asked.Job}"
            );
            Assert.AreEqual(
                asked.Tool.IsNone ? tools : tools - 1,
                game.CastleStock.Count(FoodTool),
                $"type {type}: the tool left the stock"
            );
        }
    }

    [TestMethod]
    public void AFoodBuildingWhoseToolIsMissingWaitsAsksAgainAndTheGameKeepsRunning()
    {
        // The pig farm's handler (type 0xe) asks for a tool in the made tables: an even number.
        var (game, site) = World(BuildingType.PigFarm, large: true);
        for (var tick = 0; tick < 60000 && site.Constructing; tick++)
        {
            game.Tick();
        }

        var failures = 0;
        var failed = false;
        for (var tick = 0; tick < 3000; tick++)
        {
            game.Tick();
            if (site.RequestFailed && !failed)
            {
                failures++;
            }

            failed = site.RequestFailed;
        }

        Assert.IsFalse(site.WorkerOnTheWay || site.WorkerInside, "no tool, no worker");
        Assert.IsGreaterThan(1, failures, "the building asked again after each failure");

        game.CastleStock.Add(FoodTool);
        for (var tick = 0; tick < 3000 && !site.WorkerOnTheWay; tick++)
        {
            game.Tick();
        }

        Assert.IsTrue(site.WorkerOnTheWay, "with the tool in stock the next request is served");
    }
}
