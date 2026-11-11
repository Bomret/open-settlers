using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The digger (docs/rules/digging.md): a large site on uneven ground asks for a digger, who walks out to it and works in state 8: sixteen
// passes over the node and its six neighbours, each pass lowering or raising by one the nodes that stand at the height the pass names, until
// every node of reach stands at the site's height; then the site starts, and its builder follows. Made-up tables (every step takes 100
// clock units); the program's own run is in ConstructionDiggerTests.
[TestClass]
public sealed class DiggerTests
{
    // A sawmill's site at 14,8 (a node the made-up tables judge as a site for a large building) with its flag at 15,9, which a road joins
    // to the castle's flag.
    private const int SiteX = 14;
    private const int SiteY = 8;

    private static ref Node Neighbour(Game game, int direction) =>
        ref game.Map.At(SiteX + Direction.Steps[direction].Dx, SiteY + Direction.Steps[direction].Dy);

    // The heights of the node and its six neighbours (east, south-east, south, west, north-west, north).
    private static int[] Heights(Game game) =>
        [
            game.Map.At(SiteX, SiteY).Height,
            .. Enumerable.Range(0, Direction.Count).Select(direction => Neighbour(game, direction).Height),
        ];

    // A game with a large site (a sawmill) whose ground holds the target height plus the offsets given for the node and its neighbours (east,
    // south-east, south, west, north-west, north), and a shovel in the stock.
    private static (Game Game, Building Site, int Target) World(int node, params int[] around)
    {
        var game = ConstructionWorld.Create(ConstructionTables.Tables(stepTime: 100));
        game.CastleStock.Add(ConstructionTables.DiggerTool);
        ConstructionWorld.Do(game, CommandKind.PlaceFlag, 15, 9);
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 16, 6, [2, 2, 2, 3]);
        ConstructionWorld.Site(game, BuildingType.Sawmill, SiteX, SiteY);
        var site = game.Buildings[1];
        Assert.IsTrue(site.Constructing, "the sawmill's site stands");
        var target = site.Site!.TargetHeight;
        game.Map.At(SiteX, SiteY).Height = target + node;
        for (var direction = 0; direction < Direction.Count; direction++)
        {
            Neighbour(game, direction).Height = target + around[direction];
        }

        return (game, site, target);
    }

    private static int[] Levelled(int target) => [.. Enumerable.Repeat(target, 7)];

    [TestMethod]
    public void TheDiggerLevelsTheNodeAndItsSixNeighboursToTheSitesHeightAndTheSiteStarts()
    {
        var (game, site, target) = World(-2, 2, 0, -1, 0, 1, 0);

        ConstructionWorld.Run(game, 6000);

        CollectionAssert.AreEqual(Levelled(target), Heights(game));
        Assert.IsGreaterThanOrEqualTo(1, site.Progress, "the levelled site starts");
    }

    [TestMethod]
    public void TheDiggerChangesANodeByOneEachTimeAPassNamesItsHeight()
    {
        var (game, _, target) = World(0, 2, 0, 0, 0, 0, 0);
        var seen = new List<int> { Neighbour(game, 0).Height };

        for (var tick = 0; tick < 6000; tick++)
        {
            game.Tick();
            if (Neighbour(game, 0).Height != seen[^1])
            {
                seen.Add(Neighbour(game, 0).Height);
            }
        }

        CollectionAssert.AreEqual(new[] { target + 2, target + 1, target }, seen);
    }

    [TestMethod]
    public void ALargeSiteThatIsLevelAlreadyGetsNoDigger()
    {
        var (game, site, _) = World(0, 0, 0, 0, 0, 0, 0);

        ConstructionWorld.Run(game, 300);

        Assert.IsTrue(
            ConstructionWorld.OfJob(game, Job.Digger).All(digger => digger.Activity is WaitsInCastle),
            "the castle's own digger never left"
        );
        Assert.IsGreaterThanOrEqualTo(1, site.Progress);
    }

    [TestMethod]
    public void TheBuilderStartsOnlyAfterTheGroundIsLevelled()
    {
        var (game, site, target) = World(-2, 2, 0, -1, 0, 1, 0);
        var builder = ConstructionWorld.OfJob(game, Job.Builder).Single();

        var levelled = false;
        for (var tick = 0; tick < 6000 && !levelled; tick++)
        {
            game.Tick();
            levelled = Heights(game).SequenceEqual(Levelled(target)) && site.Progress != 0;
            if (!levelled)
            {
                Assert.IsInstanceOfType<WaitsInCastle>(
                    builder.Activity,
                    "no builder leaves before the ground is levelled"
                );
            }
        }

        Assert.IsTrue(levelled);
    }

    // The node of the pass is busy (0x43e7ac): the digger takes the frame 0x57 less the node's index (6 for the east neighbour), the timer
    // 0x7f, and the phase 0; when the timer has run out he looks again, and steps onto the node once it is free.
    [TestMethod]
    public void ADiggerWhoseNodeIsBusyWaitsInPhase0AndStepsOntoItOnceItIsFree()
    {
        var (game, site, target) = World(0, 2, 0, 0, 0, 0, 0);
        var east = game.Map.Index(SiteX + 1, SiteY);
        var digger = ConstructionWorld.OfJob(game, Job.Digger).Single();
        const int stranger = 200;
        game.Movement.Occupy(east, stranger);

        Digs? waiting = null;
        for (var tick = 0; tick < 6000 && waiting is null; tick++)
        {
            game.Tick();
            waiting = digger.Activity as Digs is { Phase: 0 } digs ? digs : null;
        }

        Assert.IsNotNull(waiting, "the digger reaches the busy node");
        Assert.AreEqual(6, waiting.Node);
        Assert.AreEqual(0x57 - 6, digger.Frame);
        Assert.AreEqual((ushort)0x7f, digger.Timer);

        ConstructionWorld.Run(game, 2000);

        Assert.AreEqual(target + 2, Neighbour(game, 0).Height, "he has not stepped onto the busy node");
        Assert.AreEqual(0, ((Digs)digger.Activity).Phase, "he looks again and waits again");
        Assert.AreEqual(0x57 - 6, digger.Frame);

        game.Movement.Occupy(east, 0);
        ConstructionWorld.Run(game, 6000);

        CollectionAssert.AreEqual(Levelled(target), Heights(game));
        Assert.IsGreaterThanOrEqualTo(1, site.Progress);
    }

    [TestMethod]
    public void ANodeMoreThanEightFromTheTargetIsLeftAsItIs()
    {
        var (game, site, target) = World(0, 9, 0, 0, 0, 0, 0);

        ConstructionWorld.Run(game, 6000);

        Assert.AreEqual(target + 9, Neighbour(game, 0).Height, "no pass names a height nine above the target");
        Assert.IsGreaterThanOrEqualTo(1, site.Progress, "the digger's end starts the site all the same");
    }

    [TestMethod]
    public void WithNoDiggerAndNoShovelTheSiteWaitsAndAsksAgainOnceAShovelIsThere()
    {
        var (game, site, target) = World(-2, 2, 0, -1, 0, 1, 0);
        while (game.CastleStock.Take(ConstructionTables.DiggerTool)) { }

        // The castle's own digger is not there (the settlers enter the castle's list as their turns come, so it is emptied before every tick).
        var failed = false;
        for (var tick = 0; tick < 300; tick++)
        {
            game.Roster.ClearWaiting(Job.Digger);
            game.Tick();
            failed |= site.RequestFailed;
        }

        Assert.IsTrue(failed, "the request found no digger and no shovel and set the request-failed bit");
        Assert.IsTrue(ConstructionWorld.OfJob(game, Job.Digger).All(digger => digger.Activity is WaitsInCastle));
        Assert.AreEqual((ushort)0, site.Progress);
        Assert.AreEqual(target - 2, game.Map.At(SiteX, SiteY).Height);

        game.CastleStock.Add(ConstructionTables.DiggerTool);
        for (var tick = 0; tick < 6000; tick++)
        {
            game.Roster.ClearWaiting(Job.Digger);
            game.Tick();
        }

        Assert.AreEqual(
            2,
            ConstructionWorld.OfJob(game, Job.Digger).Length,
            "a settler without a job took the job: the site asked again"
        );
        Assert.AreEqual(0, game.CastleStock.Count(ConstructionTables.DiggerTool), "and the shovel");
        Assert.AreEqual(target, game.Map.At(SiteX, SiteY).Height);
    }
}
