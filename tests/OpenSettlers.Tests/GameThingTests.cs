using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The things of a game (Thing.Of(Game, ...)) on the map of the seed 1234,5678,9ABC: the castle at 15,5 with its flag at
// 16,6; flags at 18,6 (slot 1), 19,4 (2) and 17,3 (3); a road from the castle's flag two steps east to 18,6 and a road
// from there north twice and east to 19,4, and a road from 17,3 east and south-east to 19,4; a woodcutter's hut at 16,2 on the flag at 17,3.
[TestClass]
public sealed class GameThingTests
{
    // Made-up tables as HutTests has them: every object class is 0 except the flag's, code 1, which is 3.
    private static RuleTables Made()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;

        return new RuleTables(
            classes,
            Enumerable
                .Range(0, RuleTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount).ToArray()
        );
    }

    private static Game World()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            // No tree stands anywhere, so the woodcutter waits in his hut.
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        var game = new Game(Made(), map);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            Do(game, CommandKind.PlaceFlag, x, y);
        }

        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        Do(game, CommandKind.BuildRoad, 18, 6, [5, 5, 0]);
        Do(game, CommandKind.BuildRoad, 17, 3, [0, 1]);
        Do(game, CommandKind.PlaceBuilding, 16, 2);

        return game;
    }

    private static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    private static List<string> Lines(Game game) =>
        [.. Thing.Of(game, new NameTable(TestProgram.Made())).Select(thing => thing.Line())];

    [TestMethod]
    public void EachFlagShowsItsSlotAtItsNode()
    {
        var lines = Lines(World());

        CollectionAssert.AreEquivalent(
            new[]
            {
                "flag 16,6 slot=0 id=flag#0.1",
                "flag 18,6 slot=1 id=flag#1.1",
                "flag 19,4 slot=2 id=flag#2.1",
                "flag 17,3 slot=3 id=flag#3.1",
            },
            lines.Where(line => line.StartsWith("flag ", StringComparison.Ordinal)).ToArray()
        );
    }

    [TestMethod]
    public void EachRoadNodeShowsOnceWithTheDirectionsItsRoadsLeaveIn()
    {
        var lines = Lines(World());

        CollectionAssert.AreEquivalent(
            new[]
            {
                "road 16,6 dirs=0",
                "road 17,6 dirs=0,3",
                "road 18,6 dirs=3,5",
                "road 18,5 dirs=2,5",
                "road 18,4 dirs=0,2",
                "road 19,4 dirs=3,4",
                "road 17,3 dirs=0",
                "road 18,3 dirs=1,3",
            },
            lines.Where(line => line.StartsWith("road ", StringComparison.Ordinal)).ToArray(),
            string.Join("|", lines.Where(line => line.StartsWith("road ", StringComparison.Ordinal)))
        );
    }

    [TestMethod]
    public void TheCastleAndTheHutShowTheirRawTypeAndStatusAndTheirNames()
    {
        var lines = Lines(World());

        Assert.IsTrue(
            lines.Any(line =>
                line.StartsWith("building 15,5 type=0x18 constructing=", StringComparison.Ordinal)
                && line.EndsWith(" castle", StringComparison.Ordinal)
                && line.Contains(" status=0x", StringComparison.Ordinal)
                && line.Contains(" flag=0 worker=", StringComparison.Ordinal)
            )
        );
        Assert.IsTrue(
            lines.Any(line =>
                line.StartsWith("building 16,2 type=0x02 constructing=false ", StringComparison.Ordinal)
                && line.Contains(" flag=3 ", StringComparison.Ordinal)
                && line.EndsWith(" woodcutter-hut", StringComparison.Ordinal)
            )
        );
    }

    [TestMethod]
    public void EverySettlerShowsEveryValueTheEngineHoldsAtItsNode()
    {
        var game = World();
        for (var i = 0; i < 30; i++)
        {
            game.Tick();
        }

        var settlers = Lines(game).Where(line => line.StartsWith("settler ", StringComparison.Ordinal)).ToList();
        var first = game.Settlers[0];
        var raw = SettlerEncoder.Fields(first);

        Assert.AreEqual(game.Settlers.Count, settlers.Count);
        Assert.IsTrue(
            settlers.Contains(
                $"settler {first.X},{first.Y} job={first.Job} state=0x{raw.State:x2} player={first.Player} "
                    + $"mode=0x{raw.Mode:x2} target={raw.Target} next=0x{raw.Next:x2} heading={raw.Heading} "
                    + $"frame={first.Frame} timer={first.Timer} stamp={raw.Stamp} id=settler#0.1 "
                    + $"{NameTable.JobName(first.Job.Code)} {NameTable.StateName(first.Activity.Code)}".TrimEnd()
            ),
            string.Join("\n", settlers.Take(3))
        );
    }

    [TestMethod]
    public void TheMapViewListsTheMapObjectsWithTheirNames()
    {
        var game = World();
        var names = new NameTable(TestProgram.Made());

        var listed = Thing.Listed(false, game, game.Map, names);

        CollectionAssert.AreEqual(
            game.Map.Objects.Select(o => Thing.Of(o, names).Line()).ToArray(),
            listed.Select(thing => thing.Line()).ToArray()
        );
        Assert.IsTrue(listed.Any(thing => thing.Name is not null));
        Assert.IsTrue(listed.All(thing => thing.Kind == Thing.ObjectKind));
    }

    [TestMethod]
    public void TheMapViewListsNothingWithoutAMap()
    {
        Assert.IsEmpty(Thing.Listed(false, null, null, null));
    }

    [TestMethod]
    public void TheGameViewListsThePlacedCastleFlagsAndRoads()
    {
        var game = World();

        var lines = Thing
            .Listed(true, game, game.Map, new NameTable(TestProgram.Made()))
            .Select(thing => thing.Line())
            .ToList();

        Assert.IsTrue(lines.Any(line => line.StartsWith("building 15,5 ", StringComparison.Ordinal)));
        Assert.Contains("flag 16,6 slot=0 id=flag#0.1", lines);
        Assert.IsTrue(lines.Any(line => line.StartsWith("road 17,6 ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TheGameViewBeforeAGameListsTheMapObjects()
    {
        var map = World().Map;

        Assert.HasCount(map.Objects.Count, Thing.Listed(true, null, map, null));
    }

    [TestMethod]
    public void NoNodeShowsAFlagOrABuildingAlsoAsAMapObject()
    {
        var lines = Lines(World());

        foreach (var node in new[] { "15,5", "16,6", "18,6", "19,4", "17,3", "16,2" })
        {
            Assert.IsFalse(
                lines.Any(line => line.StartsWith($"object {node} ", StringComparison.Ordinal)),
                $"an object line at {node}"
            );
        }
    }

    [TestMethod]
    public void ABuildingTakenDownShowsItsRawStatusWithTheDemolishingBit()
    {
        var game = World();

        Do(game, CommandKind.RemoveBuilding, 16, 2);

        var hut = game.Buildings[1];
        Assert.IsTrue(hut.IsDemolishing);
        Assert.IsTrue(
            Lines(game)
                .Contains(
                    $"building 16,2 type=0x02 constructing=false status=0x{BuildingEncoder.Status(hut):x2} flag=3 worker={BuildingEncoder.Worker(hut)} id=building#1.1 flag-id=flag#3.1 woodcutter-hut"
                )
        );
    }

    [TestMethod]
    public void ABuildingBurnedDownAndAFlagTakenDownLeaveNoLine()
    {
        var game = World();

        Do(game, CommandKind.RemoveBuilding, 16, 2);
        for (var i = 0; i < 400; i++)
        {
            game.Tick();
        }

        Do(game, CommandKind.RemoveFlag, 18, 6);

        var lines = Lines(game);
        Assert.IsFalse(lines.Any(line => line.StartsWith("building 16,2 ", StringComparison.Ordinal)));
        Assert.IsFalse(lines.Any(line => line.StartsWith("flag 18,6 ", StringComparison.Ordinal)));
        Assert.IsFalse(lines.Any(line => line.StartsWith("flag -1,", StringComparison.Ordinal)));
        Assert.IsFalse(lines.Any(line => line.StartsWith("building -1,", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MapObjectsOffTheFlagsAndBuildingsStillShow()
    {
        var game = World();

        var objects = Lines(game).Count(line => line.StartsWith("object ", StringComparison.Ordinal));

        Assert.IsTrue(objects > 0);
        Assert.AreEqual(
            game.Map.Objects.Count(o =>
                !new[] { (15, 5), (16, 6), (18, 6), (19, 4), (17, 3), (16, 2) }.Contains((o.X, o.Y))
            ),
            objects
        );
    }

    // Runs the checks of script over the things the game view lists of game; the problems they report, one a line.
    private static string Checks(Game game, string script)
    {
        var things = Thing.Listed(true, game, game.Map, new NameTable(TestProgram.Made()));
        var state = new PlayState(0, 0, 1f, "1", 1, Ticks: game.TickCount, Things: things);
        var run = new PlayRun(PlayScript.Parse(script, "play/t.play"), () => state, "unused", _ => null);
        for (var i = 0; i < 5; i++)
        {
            run.Advance(1.0 / 30);
        }

        return string.Join("\n", run.Problems);
    }

    [TestMethod]
    public void ASettlerInANamedStateMatchesItsJobNameAndItsStateName()
    {
        var game = World();
        for (var i = 0; i < 30; i++)
        {
            game.Tick();
        }

        var carrier = game.Settlers.First(s =>
            s.Job == Job.Carrier && NameTable.StateName(s.Activity.Code) is not null
        );
        var carriers = game.Settlers.Count(s => s.Job == Job.Carrier);

        Assert.AreEqual(
            "",
            Checks(
                game,
                $"expect-count carrier = {carriers}\n"
                    + $"expect-at {carrier.X} {carrier.Y} carrier\n"
                    + $"expect-at {carrier.X} {carrier.Y} {NameTable.StateName(carrier.Activity.Code)}"
            )
        );
    }

    [TestMethod]
    public void TheChecksCountTheGameViewsKinds()
    {
        var game = World();

        Assert.AreEqual(
            "",
            Checks(
                game,
                "expect-count building = 2\nexpect-count flag = 4\nexpect-at 16 6 flag\nexpect-at 15 5 building"
            )
        );
    }
}
