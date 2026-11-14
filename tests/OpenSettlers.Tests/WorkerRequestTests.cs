using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The second way of the worker request (Building_RequestWorker, 0x42be63, docs/rules/construction.md): where no settler of the job waits
// in the castle, a settler without a job takes the job and the tool the job needs from the stock; with neither at hand the building's
// request-failed bit is set and the retry scan clears it. Made-up tables: the forester's worker is job 8 with the tool good 17, a job the
// castle has no settler of. The original's own run is in ConstructionBuilderTests.
[TestClass]
public sealed class WorkerRequestTests
{
    static readonly Job ForesterJob = Job.Of(8);
    static readonly Good Tool = ConstructionTables.ForesterTool;

    static Building PlaceForester(Game game)
    {
        Assert.IsTrue(game.PlaceFinishedBuilding(BuildingType.Forester, 17, 5));

        return game.Buildings[1];
    }

    [TestMethod]
    public void AJoblessSettlerTakesTheJobAndTheToolWhereNoSettlerOfTheJobWaits()
    {
        var game = ConstructionWorld.Create();
        game.CastleStock.Add(Tool);
        var tools = game.CastleStock.Count(Tool);
        var forester = PlaceForester(game);

        ConstructionWorld.Run(game, 100);

        var worker = ConstructionWorld.OfJob(game, ForesterJob).Single();
        Assert.IsTrue(forester.WorkerOnTheWay || forester.WorkerInside, "the forester's hut has its worker coming");
        Assert.AreEqual(tools - 1, game.CastleStock.Count(Tool), "the tool left the stock");
        Assert.IsFalse(worker.Activity is WaitsInCastle, "the settler left the castle");
    }

    [TestMethod]
    public void AJobWhoseSettlerWaitsInTheCastleTakesNoToolAndNoJoblessSettler()
    {
        var game = ConstructionWorld.Create();
        // The roads' carriers have taken their settlers without a job by now.
        ConstructionWorld.Run(game, 100);
        var tools = game.CastleStock.Count(Tool);
        var jobless = ConstructionWorld.OfJob(game, new Jobless()).Length;
        Assert.IsTrue(game.PlaceFinishedBuilding(BuildingType.Woodcutter, 17, 5));

        ConstructionWorld.Run(game, 600);

        Assert.AreEqual(tools, game.CastleStock.Count(Tool));
        Assert.AreEqual(jobless, ConstructionWorld.OfJob(game, new Jobless()).Length);
        Assert.AreEqual(new Woodcutter(), game.Settlers[game.Buildings[1].Worker].Job);
    }

    [TestMethod]
    public void WithNoToolInTheStockTheBuildingWaitsAndAsksAgainOnceTheToolIsThere()
    {
        var game = ConstructionWorld.Create();
        while (game.CastleStock.Take(Tool)) { }

        var forester = PlaceForester(game);
        var failed = false;
        for (var tick = 0; tick < 200; tick++)
        {
            game.Tick();
            failed |= forester.RequestFailed;
        }

        Assert.IsTrue(failed, "the request found no tool and set the request-failed bit");
        Assert.IsEmpty(ConstructionWorld.OfJob(game, ForesterJob), "no settler took the job");
        game.CastleStock.Add(Tool);

        ConstructionWorld.Run(game, 100);

        Assert.HasCount(1, ConstructionWorld.OfJob(game, ForesterJob));
        Assert.AreEqual(0, game.CastleStock.Count(Tool));
    }

    [TestMethod]
    public void WithNoJoblessSettlerTheBuildingWaitsAndTheGameGoesOn()
    {
        var game = ConstructionWorld.Create();
        game.CastleStock.Add(Tool);
        var forester = PlaceForester(game);
        var failed = false;

        for (var tick = 0; tick < 200; tick++)
        {
            // The settlers enter the castle's list as their turns come, so it is emptied before every tick.
            game.Roster.ClearWaiting(new Jobless());
            game.Tick();
            failed |= forester.RequestFailed;
        }

        Assert.IsTrue(failed);
        Assert.IsEmpty(ConstructionWorld.OfJob(game, ForesterJob));
    }
}
