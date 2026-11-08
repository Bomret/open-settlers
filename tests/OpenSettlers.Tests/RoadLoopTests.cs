using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A road that ends at the flag it started from (docs/rules/roads.md: the commit accepts it). The castle at 15,5 has its flag
// at 16,6 (slot 0). The loop leaves it east around the hexagon of the node 17,7 (17,6 18,7 18,8 17,8 16,7) and returns by a
// last step of direction 5, so both ends sit at slot 0, in directions 0 and 2. A flag on the opposite corner, 18,8, splits it
// into two roads between the same two flags: 0.0>1.5 and 1.3>0.2.
[TestClass]
public sealed class RoadLoopTests
{
    private static readonly int[] Loop = [0, 1, 2, 3, 4, 5];

    private static Game Started()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(RemovalLogicTests.Made(), map);
        Apply(game, CommandKind.PlaceCastle, 15, 5);

        return game;
    }

    private static Game WithLoop()
    {
        var game = Started();
        Apply(game, CommandKind.BuildRoad, 16, 6, Loop);

        return game;
    }

    private static void Apply(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    private static void Run(Game game, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            game.Tick();
        }
    }

    [TestMethod]
    public void ARoadFromAFlagBackToItIsBuiltWithBothEndsAtTheFlag()
    {
        var game = WithLoop();

        var road = game.Roads.Single();
        Assert.AreEqual((0, 0, 0, 2), (road.FromSlot, road.FromDirection, road.ToSlot, road.ToDirection));
        CollectionAssert.AreEqual(Loop, road.Directions.ToArray());
        Assert.AreEqual(2, game.RoadByte(0, 0) >> 4 & 7, "class 2 for six steps at the start end");
        Assert.AreEqual(2, game.RoadByte(0, 2) >> 4 & 7, "and at the other end");
        Assert.AreEqual(2 << 3, game.FlagDirectionBytes(0)[0] & 0x38, "each end names the direction of the other");
        Assert.AreEqual(0 << 3, game.FlagDirectionBytes(0)[2] & 0x38);
    }

    [TestMethod]
    public void TheLoopGetsOneCarrierThatWalksIt()
    {
        var game = WithLoop();

        Run(game, 1500);

        Assert.AreEqual(1, game.RoadByte(0, 0) & 0xf, "one carrier at the start end");
        Assert.AreEqual(1, game.RoadByte(0, 2) & 0xf, "and at the other end");
        Assert.AreEqual(1, game.Settlers.Count(s => s is { Job: Settler.CarrierJob, State: 3 or 0x42 }));
    }

    [TestMethod]
    public void AFlagOnTheLoopGivesTwoRoadsBetweenTheSameTwoFlagsEachWithACarrier()
    {
        var game = WithLoop();
        Run(game, 1500);

        Apply(game, CommandKind.PlaceFlag, 18, 8);
        Run(game, 1500);

        Assert.AreEqual(2, game.Roads.Count);
        Assert.IsTrue(game.Roads.All(r => (r.FromSlot == 0 && r.ToSlot == 1) || (r.FromSlot == 1 && r.ToSlot == 0)));
        Assert.AreEqual(Loop.Length, game.Roads.Sum(r => r.Directions.Count));
        Assert.AreEqual(1, game.RoadByte(0, 0) & 0xf, "the first half");
        Assert.AreEqual(1, game.RoadByte(1, 5) & 0xf, "the first half at the new flag");
        Assert.AreEqual(1, game.RoadByte(1, 3) & 0xf, "the second half");
        Assert.AreEqual(1, game.RoadByte(0, 2) & 0xf, "the second half at the castle flag");
        Assert.AreEqual(2, game.Settlers.Count(s => s is { Job: Settler.CarrierJob, State: 3 or 0x42 }));
    }

    [TestMethod]
    public void RemovingTheLoopFreesItsTablesOnce()
    {
        var game = WithLoop();
        var bare = Started();
        Run(game, 1500);

        Apply(game, CommandKind.RemoveRoad, 18, 8);
        Run(game, 1500);

        Assert.AreEqual(0, game.Roads.Count);
        Assert.AreEqual(0, game.RoadByte(0, 0) & 0x80, "no carrier is on the way to the road");
        Assert.AreEqual(0, game.RoadByte(0, 2) & 0x80);
        Assert.AreEqual(
            0,
            game.Settlers.Count(s => s is { Job: Settler.CarrierJob, State: 3 or 0x42 }),
            "no carrier at work"
        );
        CollectionAssert.AreEqual(TestMaps.Roads(bare.Map), TestMaps.Roads(game.Map));
    }

    [TestMethod]
    public void RemovingARoadWithSeveralNodesBesideTheClickClearsTheRoadBitsOfEveryNode()
    {
        var game = Started();
        Apply(game, CommandKind.PlaceFlag, 20, 6);
        Apply(game, CommandKind.BuildRoad, 16, 6, [0, 0, 0, 0]);
        var map = game.Map;

        Apply(game, CommandKind.RemoveRoad, 17, 6);

        // Direction 4 is left out: at the castle's flag 16,6 it is the mark of the castle at its north-west, which the
        // take-down keeps, and no road of this test uses it.
        CollectionAssert.AreEqual(
            new[] { RoadSet.Empty, RoadSet.Empty, RoadSet.Empty, RoadSet.Empty, RoadSet.Empty },
            Enumerable.Range(16, 5).Select(x => map.At(x, 6).Roads.Without(4)).ToArray()
        );
    }

    [TestMethod]
    public void TheClickRefusesTheFlagThatSplitTheLoopBecauseItsTwoRoadsGoToTheSameFlag()
    {
        var game = WithLoop();
        Apply(game, CommandKind.PlaceFlag, 18, 8);
        var roads = game.Roads.ToList();

        Apply(game, CommandKind.RemoveFlag, 18, 8);

        Assert.IsNull(game.RemovalAt(18, 8));
        CollectionAssert.AreEquivalent(roads, game.Roads.ToList());
    }

    [TestMethod]
    public void TakingDownTheFlagThatSplitTheLoopJoinsItsHalvesBackIntoTheLoop()
    {
        // The original's click refuses this flag, so the join is reached through the take-down itself.
        var game = WithLoop();
        Apply(game, CommandKind.PlaceFlag, 18, 8);

        game.TakeFlagDown(game.Map.Index(18, 8));

        var road = game.Roads.Single();
        Assert.AreEqual((0, 0), (road.FromSlot, road.ToSlot));
        Assert.AreEqual(Loop.Length, road.Directions.Count);
        CollectionAssert.AreEquivalent(
            new[] { 0, 2 },
            new[] { road.FromDirection, road.ToDirection },
            "the joined road starts at either end"
        );
        Assert.AreEqual(2, game.RoadByte(0, 0) >> 4 & 7);
    }

    [TestMethod]
    public void ARoadThatWouldCloseTheLoopInOneStepStaysImpossible()
    {
        // Out and straight back is the click that takes the last step back, so it leaves no road.
        var game = Started();
        var roads = TestMaps.Roads(game.Map);

        Apply(game, CommandKind.BuildRoad, 16, 6, [0, 3]);

        Assert.AreEqual(0, game.Roads.Count);
        CollectionAssert.AreEqual(roads, TestMaps.Roads(game.Map));
    }
}
