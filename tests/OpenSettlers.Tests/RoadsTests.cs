using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Tests of the game's own road logic, on a made-up rule table (no install): every object class is 0 except that of the
// flag, code 1, which is 3 as a road's end needs. The values the original gives are in RoadBuildingTests.
[TestClass]
public sealed class RoadsTests
{
    // The castle of the seed's map stands at 15,5 and its flag at 16,6; a flag goes on 18,6, two nodes east.
    private const int CastleX = 15;
    private const int CastleY = 5;

    private static RuleTables Made()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;

        return new RuleTables(
            classes,
            Enumerable
                .Range(0, RuleTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray()
        );
    }

    internal static Game TwoFlags()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, CastleX, CastleY));
        game.Tick();
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, 18, 6));
        game.Tick();
        Assert.AreEqual(2, game.Flags.Count);

        return game;
    }

    private static void Build(Game game, params int[] directions)
    {
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.BuildRoad, 16, 6, directions));
        game.Tick();
    }

    [TestMethod]
    public void ARoadThatEndsOnAFlagIsBuiltBetweenTheTwoFlags()
    {
        var game = TwoFlags();

        Build(game, 0, 0);

        var road = game.Roads.Single();
        Assert.AreEqual(0, road.FromSlot);
        Assert.AreEqual(0, road.FromDirection);
        Assert.AreEqual(1, road.ToSlot);
        Assert.AreEqual(3, road.ToDirection);
        CollectionAssert.AreEqual(new[] { 0, 0 }, road.Directions.ToArray());
        Assert.IsTrue(game.Map.At(16, 6).Roads.Has(0));
        Assert.AreEqual(RoadSet.Of(0).With(3), game.Map.At(17, 6).Roads);
        Assert.IsTrue(game.Map.At(18, 6).Roads.IsOnly(3));
    }

    [TestMethod]
    public void ARoadThatDoesNotEndOnAFlagLeavesNoStateBehind()
    {
        var game = TwoFlags();
        var same = TwoFlags();

        Build(game, 0);
        same.Tick();

        Assert.AreEqual(RoadStatus.Open, game.CheckRoad(16, 6, [0]));
        Assert.AreEqual(0, game.Roads.Count);
        Assert.AreEqual(same.Digest(), game.Digest());
    }

    [TestMethod]
    public void ACancelledRoadLeavesNoStateBehind()
    {
        // The stand-in interface draws a step at a time through CheckRoad and cancels by sending nothing.
        var game = TwoFlags();
        var same = TwoFlags();

        Assert.AreEqual(RoadStatus.Open, game.CheckRoad(16, 6, [0]));
        Assert.AreEqual(RoadStatus.Complete, game.CheckRoad(16, 6, [0, 0]));
        game.Tick();
        same.Tick();

        Assert.AreEqual(same.Digest(), game.Digest());
    }

    [TestMethod]
    public void AStepBackTakesTheLastStepBackAndTheRoadStillEndsOnTheFlag()
    {
        var direct = TwoFlags();
        var detour = TwoFlags();

        Build(direct, 0, 0);
        Build(detour, 0, 3, 0, 0);

        Assert.AreEqual(1, detour.Roads.Count);
        CollectionAssert.AreEqual(direct.Roads[0].Directions.ToArray(), detour.Roads[0].Directions.ToArray());
        CollectionAssert.AreEqual(TestMaps.Roads(direct.Map), TestMaps.Roads(detour.Map));
    }

    [TestMethod]
    public void AStepAfterTheRoadReachedAFlagAndAnUnknownDirectionAreRefused()
    {
        var game = TwoFlags();

        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(16, 6, [0, 0, 0]));
        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(16, 6, [6]));
        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(16, 6, [-1]));
    }

    [TestMethod]
    public void ARoadMayNotStartOnANodeWithoutAFlag()
    {
        var game = TwoFlags();

        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(17, 6, [0]));
        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(CastleX, CastleY, [1]));
    }

    [TestMethod]
    public void ARoadCannotCrossAnotherRoadOrItself()
    {
        var game = TwoFlags();
        Build(game, 0, 0);

        // The node 17,6 holds the road now: a second road may not step onto it, from the flag or from the side.
        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(16, 6, [0]));
        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(16, 6, [1, 5]));
        // Round a loop back onto a node of the road being drawn: south, south, east, north, west is 16,7 again.
        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(16, 6, [2, 2, 0, 5, 3]));
        Assert.AreEqual(RoadStatus.Open, game.CheckRoad(16, 6, [2, 2, 0, 5]));
    }

    [TestMethod]
    public void ARoadIsRefusedBeforeTheCastleStandsAndForAnotherPlayerAndOutsideTheMap()
    {
        var game = TwoFlags();
        var same = TwoFlags();
        var fresh = new Game(
            Made(),
            MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0])
        );

        game.Enqueue(new Command(game.TickCount, 1, CommandKind.BuildRoad, 16, 6, [0, 0]));
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.BuildRoad, -1, 6, [0, 0]));
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.BuildRoad, 16, 6));
        game.Tick();
        same.Tick();
        fresh.Enqueue(new Command(0, 0, CommandKind.BuildRoad, 16, 6, [0, 0]));
        fresh.Tick();
        var freshSame = new Game(
            Made(),
            MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0])
        );
        freshSame.Tick();

        Assert.AreEqual(same.Digest(), game.Digest());
        Assert.AreEqual(freshSame.Digest(), fresh.Digest());
    }

    [TestMethod]
    public void TheRoadsAreInTheDigest()
    {
        var built = TwoFlags();
        var none = TwoFlags();

        Build(built, 0, 0);
        none.Tick();

        Assert.AreNotEqual(none.Digest(), built.Digest());
    }

    [TestMethod]
    public void CommandsOfTheSameRoadAreEqualAndTheOrderOfTheirEnqueueingDoesNotMatter()
    {
        var a = new Command(5, 0, CommandKind.BuildRoad, 16, 6, new[] { 0, 0 });
        var b = new Command(5, 0, CommandKind.BuildRoad, 16, 6, new[] { 0, 0 });
        var c = new Command(5, 0, CommandKind.BuildRoad, 16, 6, new[] { 0, 1 });

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.AreNotEqual(a, c);
        Assert.AreNotEqual(a, a with { Directions = null });

        var one = TwoFlags();
        var two = TwoFlags();
        one.Enqueue(a with { Tick = 2 });
        one.Enqueue(c with { Tick = 2 });
        two.Enqueue(c with { Tick = 2 });
        two.Enqueue(b with { Tick = 2 });
        two.Enqueue(a with { Tick = 2 });
        one.Tick();
        one.Tick();
        one.Tick();
        two.Tick();
        two.Tick();
        two.Tick();

        Assert.AreEqual(one.Digest(), two.Digest());
    }
}
