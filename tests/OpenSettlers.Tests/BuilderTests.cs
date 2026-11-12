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
    private static (Game Game, Building Site) World(int planksOnSite = 2, bool everyPassTurns = false)
    {
        var game = ConstructionWorld.Create(ConstructionTables.Tables(stepTime: 300), everyPassTurns: everyPassTurns);
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

    // With the turns of every pass, no settler the view draws stands still for more than a pass: the builder on his way out, at work,
    // and on his way home, and the carriers. The slice turns alone leave the figures standing for passes.
    [TestMethod]
    public void NoDrawnSettlerStallsWhenEveryPassGivesATurn()
    {
        var (on, _) = World(everyPassTurns: true);
        var (off, _) = World(everyPassTurns: false);

        var (longest, seen) = ConstructionWorld.LongestStall(on, 3000);

        Assert.IsGreaterThan(1, ConstructionWorld.LongestStall(off, 3000).Longest, "the slice turns alone stall");
        Assert.IsLessThanOrEqualTo(1, longest);
        CollectionAssert.IsSubsetOf(new[] { typeof(Walks), typeof(Builds) }, seen.ToList());
    }

    // The view draws a site by its progress word (SiteStages), so a blow is a change of the object layer the view must redraw.
    [TestMethod]
    public void TheObjectLayerWatchReportsEveryBlowOnASite()
    {
        var (game, site) = World();
        var watch = new OpenSettlers.MapView.ObjectLayerWatch();
        watch.Changed(game);
        var blows = 0;

        for (var tick = 0; tick < 3000 && site.Constructing; tick++)
        {
            var before = site.Progress;
            game.Tick();
            var changed = watch.Changed(game);
            if (site.Progress != before)
            {
                blows++;
                Assert.IsTrue(
                    changed,
                    $"tick {game.TickCount}: the progress went from {before:x4} to {site.Progress:x4}"
                );
            }
        }

        Assert.IsGreaterThan(1, blows, "the builder made his blows");
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
        Assert.IsFalse(site.Constructing);
        var builder = ConstructionWorld.OfJob(game, Job.Builder).Single();
        Assert.IsInstanceOfType<WaitsInCastle>(builder.Activity, "the builder entered the castle again");
    }

    [TestMethod]
    public void TheFinishingBlowTakesTheBuilderOutOfTheSiteAndHeLeavesIt()
    {
        var (game, site) = World();
        var builder = ConstructionWorld.OfJob(game, Job.Builder).Single();
        var inside = false;
        for (var tick = 0; tick < 3000 && site.Constructing; tick++)
        {
            game.Tick();
            inside |= site.WorkerInside && site.Worker == game.Settlers.ToList().IndexOf(builder);
        }

        Assert.IsTrue(inside, "while the builder works the site holds him as its worker");
        Assert.IsFalse(site.Constructing);
        Assert.IsFalse(site.WorkerInside, "the worker bit goes with the building's end");
        Assert.AreEqual(0, site.Worker, "so does the worker's slot");
        Assert.IsNotInstanceOfType<Builds>(builder.Activity, "he no longer builds");
        Assert.IsTrue(
            builder.Activity is LeavesSite or WaitsOutStep { GoingTo: ToStorage },
            "he waits at the door for the flag, or has taken the step onto it, on his way to the castle"
        );
    }

    // A forester's site that needs one plank and one stone, in the order plank first, then stone (the order word 0b10), with the plank
    // on site and the stone as the test sets it. The planks' fill byte (FillLow, byte 8) and the stones' (FillHigh, byte 9) each hold the
    // goods on site in the high nibble.
    private static (Game Game, Building Site) StoneWorld(ushort order = 0b10, int plankOnSite = 1, int stoneOnSite = 0)
    {
        var needs = new byte[2 * RuleTables.BuildingTypeCount];
        needs[2 * BuildingType.Forester.Code] = 1;
        needs[(2 * BuildingType.Forester.Code) + 1] = 1;
        var materialOrder = new ushort[RuleTables.BuildingTypeCount];
        materialOrder[BuildingType.Forester.Code] = order;
        var game = ConstructionWorld.Create(
            ConstructionTables.Tables(stepTime: 300, needs: needs, materialOrder: materialOrder)
        );
        ConstructionWorld.Site(game, BuildingType.Forester, SiteX, SiteY);
        var site = game.Buildings[1];
        site.FillLow = plankOnSite << 4;
        site.FillHigh = stoneOnSite << 4;

        return (game, site);
    }

    [TestMethod]
    public void TheBuilderWaitsForTheStoneThatTheOrderNamesAfterThePlankAndTakesItFromTheStonesFillByte()
    {
        var (game, site) = StoneWorld();

        var seen = Progress(game, site, 1500);

        Assert.AreEqual(Words(Enumerable.Range(0, 9).Select(blow => (ushort)(1 + 0x1000 * blow))), Words(seen));
        Assert.AreEqual(0, site.FillLow, "the plank went out of the planks' fill byte");
        Assert.AreEqual(0, site.Site!.Planks);
        Assert.AreEqual(1, site.Site.Stones, "no stone was taken");

        site.FillHigh = 0x10;
        for (var tick = 0; tick < 1500 && site.Progress <= 0x8001; tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(0, site.FillHigh, "the stone went out of the stones' fill byte");
        Assert.AreEqual(0, site.Site!.Stones);
        Assert.AreEqual(0, site.Site.Planks);
        Assert.IsTrue(site.Constructing, "the eight blows of the stone are not yet made");
        Assert.IsGreaterThan(0x8001, site.Progress);
    }

    [TestMethod]
    public void AStoneOnSiteDoesNotLetTheBuilderGoOnWhereTheOrderNamesAPlankNext()
    {
        var (game, site) = StoneWorld(plankOnSite: 0, stoneOnSite: 1);

        var seen = Progress(game, site, 1500);

        Assert.AreEqual(Words([1]), Words(seen), "he waits for the plank, with no blow");
        Assert.AreEqual(0x10, site.FillHigh, "the stone stays on site");
        Assert.AreEqual(1, site.Site!.Stones);
        Assert.AreEqual(1, site.Site.Planks);
    }

    [TestMethod]
    public void AStoneComesFirstWhereTheOrderWordHasItsLowBitSet()
    {
        var (game, site) = StoneWorld(order: 0b01, plankOnSite: 1, stoneOnSite: 0);

        var seen = Progress(game, site, 1500);

        Assert.AreEqual(Words([1]), Words(seen), "the plank on site is no use before the stone");

        site.FillHigh = 0x10;
        seen = Progress(game, site, 1500);

        Assert.AreEqual((ushort)0, seen[^1], "the stone, then the plank, build it");
        Assert.AreEqual(0, site.FillHigh);
        Assert.AreEqual(0, site.FillLow);
        Assert.IsFalse(site.Constructing);
    }

    [TestMethod]
    public void TheFinishedBuildingAsksForItsWorkerAndHeWalksOutToIt()
    {
        var (game, site) = World();
        game.CastleStock.Add(ConstructionTables.ForesterTool);

        // The forester goes into his building and out again to his work, so the test watches for the moment he stands in the doorway.
        var inside = false;
        for (var tick = 0; tick < 6000 && !inside; tick++)
        {
            game.Tick();
            inside = ConstructionWorld.OfJob(game, Job.Of(8)).Any(worker => (worker.X, worker.Y) == (SiteX, SiteY));
        }

        Assert.IsTrue(site.WorkerInside || site.WorkerOnTheWay);
        Assert.IsTrue(inside, "the worker stands in his building");
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
