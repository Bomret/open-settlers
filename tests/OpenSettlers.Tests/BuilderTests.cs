using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The builder (docs/rules/construction.md): every site asks for a builder, who walks out to it and builds in state 9, per material 8
// blows that each add a step to the progress word, in the order the type's order word gives; on the carry the building is finished and the
// builder goes home through state 0x4a. Made-up tables (every step takes 100 clock units, a blow of the forester or the woodcutter adds
// 0x1000, so sixteen build them); the program's own numbers are in ConstructionBuilderTests.
[TestClass]
public sealed class BuilderTests
{
    private const int SiteX = 17;
    private const int SiteY = 5;

    // A game with a forester's site at 17,5 whose planks lie on site: the hand-out of the castle is not part of these tests. A step takes
    // 300 clock units, more than the 128 between two turns of a settler, so a builder's turn makes one blow at most.
    private static (Game Game, Building Site) World(int planksOnSite = 2)
    {
        var game = ConstructionWorld.Create(ConstructionTables.Tables(stepTime: 300));
        ConstructionWorld.Site(game, BuildingType.Forester, SiteX, SiteY);
        var site = game.Buildings[1];
        site.FillLow = planksOnSite << 4;

        return (game, site);
    }

    private static string Words(IEnumerable<ushort> words) =>
        string.Join(",", words.Select(word => word.ToString("x4")));

    // The distinct values of the site's progress word the ticks give, in order.
    private static List<ushort> Progress(Game game, Building site, int ticks, System.Action? each = null)
    {
        var seen = new List<ushort> { site.Progress };
        for (var tick = 0; tick < ticks; tick++)
        {
            each?.Invoke();
            game.Tick();
            if (site.Progress != seen[^1])
            {
                seen.Add(site.Progress);
            }
        }

        return seen;
    }

    [TestMethod]
    public void ASmallSiteWithItsPlanksIsBuiltInSixteenBlowsOfOneStepEach()
    {
        var (game, site) = World();

        var seen = Progress(game, site, 3000);

        // The progress word starts at 1 (a small site), each blow adds 0x1000, and the carry out of the word finishes the building.
        var expected = Enumerable.Range(0, 16).Select(blow => (ushort)(1 + 0x1000 * blow)).Append((ushort)0).ToList();
        Assert.AreEqual(Words(expected), Words(seen));
        Assert.IsFalse(site.Constructing);
        Assert.IsNull(site.Site);
    }

    [TestMethod]
    public void TheBuildingIsFinishedItsFlagAsksForNothingAndItsBuilderGoesHome()
    {
        var (game, site) = World();
        game.CastleStock.Add(ConstructionTables.ForesterTool);

        ConstructionWorld.Run(game, 3000);

        var flag = game.Flags[site.Flag];
        Assert.IsNull(flag.RequestOf(RequestWord.First).Asks);
        Assert.IsNull(flag.RequestOf(RequestWord.Second).Asks);
        Assert.IsFalse(site.WorkerInside && site.Worker == 0);
        var builder = ConstructionWorld.OfJob(game, Job.Builder).Single();
        Assert.IsInstanceOfType<WaitsInCastle>(builder.Activity, "the builder entered the castle again");
    }

    [TestMethod]
    public void TheFinishedBuildingAsksForItsWorkerAndHeWalksOutToIt()
    {
        var (game, site) = World();
        game.CastleStock.Add(ConstructionTables.ForesterTool);

        ConstructionWorld.Run(game, 6000);

        var worker = ConstructionWorld.OfJob(game, new Job(8)).Single();
        Assert.IsTrue(site.WorkerInside || site.WorkerOnTheWay);
        Assert.AreEqual((SiteX, SiteY), (worker.X, worker.Y), "the worker stands in his building");
    }

    [TestMethod]
    public void ASiteWithItsMaterialsAndNoBuilderDoesNotProgress()
    {
        var (game, site) = World();
        var failed = false;

        Progress(
            game,
            site,
            400,
            () =>
            {
                // No settler of the builder's job waits, no settler without a job waits, and the stock holds no tool.
                game.Roster.ClearWaiting(Job.Builder);
                game.Roster.ClearWaiting(Job.Jobless);
                failed |= site.RequestFailed;
            }
        );

        Assert.AreEqual(1, site.Progress);
        Assert.IsTrue(failed, "the request found no builder");
        Assert.IsTrue(site.Constructing);
        Assert.IsEmpty(
            ConstructionWorld.OfJob(game, Job.Builder).Where(settler => settler.Activity is not WaitsInCastle)
        );
    }

    [TestMethod]
    public void AJoblessSettlerWithAHammerFromTheStockBuildsWhereTheBuilderIsBusy()
    {
        var game = ConstructionWorld.Create();
        game.CastleStock.Add(ConstructionTables.BuilderTool);
        var tools = game.CastleStock.Count(ConstructionTables.BuilderTool);
        ConstructionWorld.Site(game, BuildingType.Forester, SiteX, SiteY);
        // The second site's flag at 17,3 joins the flag at 19,4 by a road east and south-east.
        ConstructionWorld.Site(game, BuildingType.Woodcutter, 16, 2);
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 17, 3, [0, 1]);
        Assert.HasCount(3, game.Buildings, "the castle and the two sites");

        ConstructionWorld.Run(game, 600);

        Assert.AreEqual(2, ConstructionWorld.OfJob(game, Job.Builder).Length, "the castle's builder and a made one");
        Assert.AreEqual(tools - 1, game.CastleStock.Count(ConstructionTables.BuilderTool));
        Assert.IsTrue(game.Buildings.Skip(1).All(building => building.WorkerInside || building.WorkerOnTheWay));
    }

    [TestMethod]
    public void TheBuilderStopsWhereTheNextMaterialIsNotOnSiteAndGoesOnWhenItArrives()
    {
        var (game, site) = World(planksOnSite: 1);

        var seen = Progress(game, site, 1500);

        // One plank gives eight blows, a half of the word; the builder then waits for the second.
        Assert.AreEqual(Words(Enumerable.Range(0, 9).Select(blow => (ushort)(1 + 0x1000 * blow))), Words(seen));
        Assert.IsTrue(site.Constructing);

        site.FillLow = 0x10;
        seen = Progress(game, site, 1500);

        Assert.AreEqual((ushort)0, seen[^1], "the second plank lets him finish");
        Assert.IsFalse(site.Constructing);
    }
}
