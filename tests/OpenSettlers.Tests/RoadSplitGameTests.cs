using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Tests of the game's own split of a road by a flag (docs/rules/roads.md#a-flag-on-a-road) on made-up tables (no install). The
// values the original gives are in RoadSplitTests. The map of the seed 1234,5678,9ABC has its castle at 15,5 with its flag at
// 16,6 (slot 0); a flag at 20,6 (slot 1) ends the road east of it, and the flag that splits the road goes to 18,6 (slot 2).
[TestClass]
public sealed class RoadSplitGameTests
{
    private static Game Started(int castleX, int castleY, int endX, int endY, params int[] road) =>
        Started(null, castleX, castleY, endX, endY, road);

    // The road from the castle's flag to the flag at the end; beforeRoad changes the map after the flags stand and before the road.
    private static Game Started(
        Action<Map>? beforeRoad,
        int castleX,
        int castleY,
        int endX,
        int endY,
        params int[] road
    )
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(RemovalLogicTests.Made(), map);
        Apply(game, CommandKind.PlaceCastle, castleX, castleY);
        Apply(game, CommandKind.PlaceFlag, endX, endY);
        beforeRoad?.Invoke(map);
        game.Enqueue(
            new Command(
                game.TickCount,
                0,
                CommandKind.BuildRoad,
                (castleX + 1) & (map.Size.Width - 1),
                (castleY + 1) & (map.Size.Height - 1),
                road
            )
        );
        game.Tick();
        Assert.AreEqual(1, game.Roads.Count, "the road is built");

        return game;
    }

    private static Game EastRoad() => Started(15, 5, 20, 6, 0, 0, 0, 0);

    private static void Apply(Game game, int kind, int x, int y)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y));
        game.Tick();
    }

    private static void Run(Game game, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            game.Tick();
        }
    }

    private static int[] Path(Game game) =>
        [.. game.Roads.OrderBy(road => road.FromSlot).SelectMany(road => road.Directions)];

    [TestMethod]
    public void AFlagOnARoadGivesTwoRoadsThatMeetAtItAndFollowTheOldPath()
    {
        var game = EastRoad();

        Apply(game, CommandKind.PlaceFlag, 18, 6);

        Assert.AreEqual(2, game.Roads.Count);
        Assert.AreEqual("0.0>2.3 00 False", Show(game.Roads.Single(road => road.FromSlot == 0)), "castle half");
        Assert.AreEqual("2.0>1.3 00 False", Show(game.Roads.Single(road => road.FromSlot == 2)), "far half");
        CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, Path(game));
    }

    [TestMethod]
    public void TheTablesAtTheThreeFlagsNameEachOtherAndHoldTheLengthOfTheirHalf()
    {
        var game = EastRoad();

        Apply(game, CommandKind.PlaceFlag, 18, 6);

        // The direction byte holds the direction the road leaves the far end by in bits 3 to 5; the road byte the length class.
        Assert.AreEqual(new Direction(3), game.Flags[0].Side(0).FarDirection, "castle flag to the new flag");
        Assert.AreEqual(new Direction(0), game.Flags[2].Side(3).FarDirection, "new flag to the castle flag");
        Assert.AreEqual(new Direction(3), game.Flags[2].Side(0).FarDirection, "new flag to the far flag");
        Assert.AreEqual(new Direction(0), game.Flags[1].Side(3).FarDirection, "far flag to the new flag");
        Assert.AreEqual(new LengthClass(0), game.Flags[2].Side(0).Length, "a half of 2 steps is class 0");
    }

    [TestMethod]
    public void ARoadAcrossTheMapEdgeSplitsAtANodeOnTheOtherSide()
    {
        // The castle at 1,8 has its flag at 2,9; the road runs west over the edge to 30,9, and the flag goes on 0,9.
        var game = Started(1, 8, 30, 9, 3, 3, 3, 3);

        Apply(game, CommandKind.PlaceFlag, 0, 9);

        Assert.AreEqual(2, game.Roads.Count);
        Assert.AreEqual("0.3>2.0 33 False", Show(game.Roads.Single(road => road.FromSlot == 0)));
        Assert.AreEqual("2.3>1.0 33 False", Show(game.Roads.Single(road => road.FromSlot == 2)));
    }

    // The triangles beside the steps from 16,6 to 20,6 are water (the lower triangle of a node and the upper of its north node,
    // nibbles below 4); the other triangles around the nodes stay land, so a flag is allowed on the road.
    private static Game WaterRoad() =>
        Started(
            map =>
            {
                for (var x = 16; x < 20; x++)
                {
                    map.At(x, 6).Lower = new Terrain(map.At(x, 6).Lower.Code & 3);
                    map.At(x, 5).Upper = new Terrain(map.At(x, 5).Upper.Code & 3);
                }
            },
            15,
            5,
            20,
            6,
            0,
            0,
            0,
            0
        );

    private static string Show(Road road) =>
        $"{road.FromSlot}.{road.FromDirection}>{road.ToSlot}.{road.ToDirection} {string.Concat(road.Directions)} {road.Water}";

    // A road as its two ends and its path, read from the lower slot: the joined road may start at either end.
    private static string Describe(Road road) =>
        road.FromSlot <= road.ToSlot
            ? $"{road.FromSlot}.{road.FromDirection}>{road.ToSlot}.{road.ToDirection} {string.Concat(road.Directions)} {road.Water}"
            : $"{road.ToSlot}.{road.ToDirection}>{road.FromSlot}.{road.FromDirection} {string.Concat(road.Directions.Reverse().Select(step => (step + 3) % 6))} {road.Water}";

    [TestMethod]
    public void AWaterRoadSplitsIntoTwoWaterRoads()
    {
        var game = WaterRoad();
        Assert.IsTrue(game.Roads.Single().Water, "the road is a water road");

        Apply(game, CommandKind.PlaceFlag, 18, 6);

        Assert.AreEqual(2, game.Roads.Count);
        Assert.IsTrue(game.Roads.All(road => road.Water));
    }

    [TestMethod]
    public void RemovingTheFlagThatSplitARoadJoinsTheHalvesIntoOneRoadWithTheOldPath()
    {
        var game = EastRoad();
        var before = game.Roads.Single();
        var bytes = (FlagEncoder.RoadByte(game.Flags[0].Side(0)), FlagEncoder.RoadByte(game.Flags[1].Side(3)));
        Apply(game, CommandKind.PlaceFlag, 18, 6);

        Apply(game, CommandKind.RemoveFlag, 18, 6);

        Assert.AreEqual(Describe(before), Describe(game.Roads.Single()));
        Assert.AreEqual(
            bytes,
            (FlagEncoder.RoadByte(game.Flags[0].Side(0)), FlagEncoder.RoadByte(game.Flags[1].Side(3)))
        );
    }

    [TestMethod]
    public void TwoWaterRoadsJoinIntoAWaterRoad()
    {
        // The original's click refuses a flag between water roads, so the join is reached through the take-down itself.
        var game = WaterRoad();
        Apply(game, CommandKind.PlaceFlag, 18, 6);

        game.TakeFlagDown(game.Map.Index(18, 6));

        Assert.AreEqual("0.0>1.3 0000 True", Describe(game.Roads.Single()));
    }

    [TestMethod]
    public void AFlagBesideAFlagOnTheRoadIsRefusedAndTheRoadStaysAsItWas()
    {
        var game = EastRoad();
        var digest = game.Digest();

        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, 17, 6));
        game.ApplyCommands();

        Assert.AreEqual(digest, game.Digest());
        Assert.AreEqual(1, game.Roads.Count);
    }

    [TestMethod]
    public void TheHalfWithoutACarrierAsksForOneAndTheOldCarrierKeepsItsHalf()
    {
        var game = EastRoad();
        Run(game, 600);
        Assert.AreEqual(1, game.Flags[0].Side(0).Carriers, "the road holds its carrier");
        Apply(game, CommandKind.PlaceFlag, 18, 6);
        Assert.AreEqual(
            1,
            game.Flags[2].Side(3).Carriers + game.Flags[2].Side(0).Carriers,
            "one half holds the carrier"
        );

        Run(game, 600);

        Assert.AreEqual(1, game.Flags[2].Side(0).Carriers, "the far half has a carrier");
        Assert.AreEqual(1, game.Flags[2].Side(3).Carriers, "the castle half has a carrier");
        Assert.AreEqual(
            2,
            game.Settlers.Count(settler =>
                settler is { Job.Code: Job.CarrierCode, Activity: WalksRoad or WaitsAtFlag }
            ),
            "two carriers work the two roads"
        );
    }

    [TestMethod]
    public void ACarrierThatCarriesAGoodWhenTheRoadSplitsStillCarriesIt()
    {
        var game = EastRoad();
        game.LayGood(0, 0, 7, destination: 1);
        for (
            var tick = 0;
            tick < 3000 && !game.Settlers.Any(settler => settler is { Activity: WalksRoad { Load.Good.Code: 7 } });
            tick++
        )
        {
            game.Tick();
        }

        Assert.IsTrue(
            game.Settlers.Any(settler => settler is { Activity: WalksRoad { Load.Good.Code: 7 } }),
            "the carrier carries the good"
        );
        var total = game.Map.Resource1Total;
        Apply(game, CommandKind.PlaceFlag, 18, 6);

        Assert.AreEqual(
            1,
            game.Settlers.Count(settler => settler is { Activity: WalksRoad { Load.Good.Code: 7 } }),
            "still carried once"
        );
        Assert.AreEqual(total, game.Map.Resource1Total);
    }

    [TestMethod]
    public void TheSplitRecordsItsEventsWithTheCauseOfTheFlagAndNamesTheNewCarrier()
    {
        var game = EastRoad();
        Run(game, 600);
        Apply(game, CommandKind.PlaceFlag, 18, 6);
        var cause = Cause.Placed(game.History.FlagId(2));

        Run(game, 600);

        var split = game.History.EventsOf(game.History.FlagId(2)).Single(e => e.Type == EventType.SplitRoads);
        Assert.AreEqual(cause, split.Cause);
        Assert.AreEqual(
            $"{game.History.FlagId(1)} {game.History.FlagId(0)}",
            split.Other,
            "the far flags, the lower direction first"
        );
        Assert.IsTrue(
            game.History.EventsOf(game.History.FlagId(1))
                .Any(e => e.Type == EventType.SplitRoads && e.Other == game.History.FlagId(2).ToString())
        );
        var requested = game
            .History.Events.All.Where(e =>
                e.Type == EventType.CarrierRequested && e.Thing.Kind == ThingId.SettlerKind && e.Tick > split.Tick
            )
            .ToList();
        Assert.AreEqual(1, requested.Count, "one carrier is asked for the half without one");
        Assert.AreEqual(cause, requested[0].Cause);
    }

    // The hut at 17,5 has its flag on the road node 18,6 (node state 6).
    [TestMethod]
    public void AHutWhoseFlagLandsOnARoadIsPlacedAndItsFlagSplitsTheRoad()
    {
        var game = EastRoad();

        Apply(game, CommandKind.PlaceBuilding, 17, 5);

        Assert.AreEqual(2, game.Buildings.Count(building => !building.IsFree), "the castle and the hut");
        Assert.AreEqual(3, game.Flags.Count);
        Assert.AreEqual("0.0>2.3 00 False", Show(game.Roads.Single(road => road.FromSlot == 0)));
        Assert.AreEqual("2.0>1.3 00 False", Show(game.Roads.Single(road => road.FromSlot == 2)));
        Assert.AreEqual(2, game.Buildings[1].Flag, "the hut is tied to the flag that split the road");
        var split = game.History.EventsOf(game.History.FlagId(2)).Single(e => e.Type == EventType.SplitRoads);
        Assert.AreEqual(Cause.Placed(game.History.FlagId(2)), split.Cause);
    }

    [TestMethod]
    public void AHutOnARoadSplitsItAsTheFlagButtonDoesAndSetsItsMarkAfterTheSplit()
    {
        var byButton = EastRoad();
        Apply(byButton, CommandKind.PlaceFlag, 18, 6);
        var byHut = EastRoad();

        Apply(byHut, CommandKind.PlaceBuilding, 17, 5);

        Assert.AreEqual(Describe(byButton.Roads[0]), Describe(byHut.Roads[0]));
        Assert.AreEqual(Describe(byButton.Roads[1]), Describe(byHut.Roads[1]));
        var roads = byHut.Map.At(18, 6).Roads;
        Assert.IsTrue(roads.Has(4), "the hut's mark at its flag");
        Assert.AreEqual(RoadSet.Of(0).With(3), roads.Without(4), "the road bits of the two halves");
    }

    [TestMethod]
    public void AHutWhereTheFlagMayNotStandIsRefusedAndNothingChanges()
    {
        var game = EastRoad();
        var digest = game.Digest();

        // The flag node 17,6 is beside the castle's flag at 16,6.
        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceBuilding, 16, 5));
        game.ApplyCommands();

        Assert.AreEqual(digest, game.Digest());
        Assert.AreEqual(1, game.Roads.Count);
    }

    // The hut's flag node 18,6 holds a road (node state 6), but the hut's own node 17,5 is a site for a flag only (site 1).
    [TestMethod]
    public void AHutOnARoadNodeWhoseSiteIsTooSmallIsRefusedAndNothingChanges()
    {
        var game = EastRoad();
        game.Map.At(17, 5).Upper = new Terrain(0);
        game.Map.At(17, 5).Lower = new Terrain(0);
        var site = BuildSite.Evaluate(TestProgram.Made(), game.Map, 17, 5, 0, hasCastle: true);
        Assert.AreEqual((6, 1), (site.Node, site.Site), "a road on the flag node, a site for a flag only");
        var digest = game.Digest();

        game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceBuilding, 17, 5));
        game.ApplyCommands();

        Assert.AreEqual(digest, game.Digest());
        Assert.AreEqual(1, game.Roads.Count);
        Assert.AreEqual(2, game.Flags.Count);
    }

    [TestMethod]
    public void TheMarkOfAHutAtItsFlagIsNoRoadForALaterRoadAtThatFlag()
    {
        var game = EastRoad();
        Apply(game, CommandKind.PlaceBuilding, 17, 5);
        Apply(game, CommandKind.PlaceFlag, 18, 8);

        game.Enqueue(new Command(game.TickCount, 0, CommandKind.BuildRoad, 18, 6, [2, 2]));
        game.Tick();

        Assert.AreEqual(3, game.Roads.Count, "a road leaves the hut's flag along a direction the mark does not take");
        Assert.IsTrue(game.Map.At(18, 6).Roads.Has(4), "the mark stays");
    }

    [TestMethod]
    public void RemovingTheHutAndThenItsFlagJoinsTheRoadBackIntoOne()
    {
        var game = EastRoad();
        var before = game.Roads.Single();
        Apply(game, CommandKind.PlaceBuilding, 17, 5);
        Apply(game, CommandKind.RemoveBuilding, 17, 5);

        Apply(game, CommandKind.RemoveFlag, 18, 6);

        Assert.AreEqual(Describe(before), Describe(game.Roads.Single()));
    }

    // The click refuses the flag while the hut stands (ClickClass), so the join is reached through the take-down itself.
    [TestMethod]
    public void TheMarkOfAStandingHutAtItsFlagIsNoRoadWhenTheFlagJoinsTheRoad()
    {
        var game = EastRoad();
        var before = game.Roads.Single();
        Apply(game, CommandKind.PlaceBuilding, 17, 5);
        var node = game.Map.Index(18, 6);
        Assert.IsTrue(game.Map.At(node).Roads.Has(4), "the hut's mark is set");

        game.TakeFlagDown(node);

        Assert.AreEqual(Describe(before), Describe(game.Roads.Single()));
    }

    [TestMethod]
    public void BothHalvesOfARoadSplitByAHutHoldACarrier()
    {
        var game = EastRoad();
        Run(game, 600);
        Apply(game, CommandKind.PlaceBuilding, 17, 5);

        Run(game, 900);

        Assert.AreEqual(1, game.Flags[2].Side(0).Carriers, "the far half has a carrier");
        Assert.AreEqual(1, game.Flags[2].Side(3).Carriers, "the castle half has a carrier");
    }

    [TestMethod]
    public void ReplayingTheSameCommandsGivesTheSameState()
    {
        Game Play()
        {
            var game = EastRoad();
            Run(game, 300);
            Apply(game, CommandKind.PlaceFlag, 18, 6);
            Run(game, 300);

            return game;
        }

        Assert.AreEqual(Play().Digest(), Play().Digest());
    }
}
