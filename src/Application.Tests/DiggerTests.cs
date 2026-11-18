using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The digger whose node is busy (docs/rules/digging.md), which occupies the node through the settlers' movement: made-up tables (every
// step takes 100 clock units) on the bench's flat map. The castle stands at 15,5 with its flag at 16,6, the flags 18,6, 19,4, and 17,3
// with roads from the castle's flag east to 18,6 and from there to 19,4, as ConstructionWorld lays them; a sawmill's site at 14,8 has its
// flag at 15,9, which a road joins to the castle's flag. The rest of the class is in src/MapView.Tests.
[TestClass]
public sealed class DiggerTests
{
    const int SiteX = 14;
    const int SiteY = 8;

    static ref Node Neighbour(Game game, int direction) =>
        ref game.Map.At(SiteX + Direction.Steps[direction].Dx, SiteY + Direction.Steps[direction].Dy);

    static int[] Heights(Game game) =>
        [
            game.Map.At(SiteX, SiteY).Height,
            .. Enumerable.Range(0, Direction.Count).Select(direction => Neighbour(game, direction).Height),
        ];

    static int[] Levelled(int target) => [.. Enumerable.Repeat(target, 7)];

    // The site's ground holds the target height plus the offsets given for the node and its neighbours (east, south-east, south, west,
    // north-west, north), and a shovel is in the stock.
    static (Game Game, Building Site, int Target) World(int node, params int[] around)
    {
        var game = GameBench.Create(ConstructionTables.Tables(stepTime: 100), GameBench.Flat());
        GameBench.Do(game, CommandKind.PlaceCastle, 15, 5);
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            GameBench.Do(game, CommandKind.PlaceFlag, x, y);
        }

        GameBench.Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        GameBench.Do(game, CommandKind.BuildRoad, 18, 6, [5, 5, 0]);
        game.CastleStock.Add(ConstructionTables.DiggerTool);
        GameBench.Do(game, CommandKind.PlaceFlag, 15, 9);
        GameBench.Do(game, CommandKind.BuildRoad, 16, 6, [2, 2, 2, 3]);
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = SiteX,
                Y = SiteY,
                Building = BuildingType.Sawmill,
            }
        );
        game.Tick();
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

    // The node of the pass is busy (0x43e7ac): the digger takes the frame 0x57 less the node's index (6 for the east neighbour), the timer
    // 0x7f, and the phase 0; when the timer has run out he looks again, and steps onto the node once it is free.
    [TestMethod]
    public void ADiggerWhoseNodeIsBusyWaitsInPhase0AndStepsOntoItOnceItIsFree()
    {
        var (game, site, target) = World(0, 2, 0, 0, 0, 0, 0);
        var east = game.Map.Index(SiteX + 1, SiteY);
        var digger = game.Settlers.Skip(1).Single(settler => settler.Job == new Digger());
        const int Stranger = 200;
        game.Movement.Occupy(east, Stranger);

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

        GameBench.Run(game, 2000);

        Assert.AreEqual(target + 2, Neighbour(game, 0).Height, "he has not stepped onto the busy node");
        Assert.AreEqual(0, ((Digs)digger.Activity).Phase, "he looks again and waits again");
        Assert.AreEqual(0x57 - 6, digger.Frame);

        game.Movement.Occupy(east, 0);
        GameBench.Run(game, 6000);

        CollectionAssert.AreEqual(Levelled(target), Heights(game));
        Assert.IsGreaterThanOrEqualTo(1, site.Progress);
    }
}
