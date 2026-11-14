using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The events the removal rules record (ADR 0011, docs/rules/removal.md), on the map of the seed 1234,5678,9ABC with the castle
// at 15,5: flags at 18,6 (slot 1), 19,4 (2), 17,3 (3), roads between them, a hut at 16,2.
[TestClass]
public sealed class ThingHistoryRemovalTests
{
    static RuleTables Made()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;

        return new RuleTables(
            classes,
            [
                .. Enumerable
                    .Range(0, RuleTables.StartStockRows)
                    .Select(row =>
                        Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray()
                    ),
            ],
            [.. Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount)],
            [.. Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount)],
            [.. Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount)],
            workers: TestProgram.MadeWorkers()
        );
    }

    static Game NewGame()
    {
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        for (var index = 0; index < map.Count; index++)
        {
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        return GameFactory.Create(Made(), map);
    }

    static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = kind,
                X = x,
                Y = y,
                Directions = directions,
            }
        );
        game.Tick();
    }

    static Game World()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            Do(game, CommandKind.PlaceFlag, x, y);
        }

        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        Do(game, CommandKind.BuildRoad, 18, 6, [5, 5, 0]);
        Do(game, CommandKind.BuildRoad, 17, 3, [0, 1]);
        Huts.PlaceAndTick(game, 16, 2);
        for (var i = 0; i < 400; i++)
        {
            game.Tick();
        }

        return game;
    }

    static string Text(Game game) =>
        string.Join("\n", game.History.Events.All.Select(e => $"{e.Tick} {e.Type} {e.Thing} {e.Cause} {e.Other}"));

    [TestMethod]
    public void RazingAFlagWithARoadRecordsTheFlagTheCarrierAndTheFlagsEndWithCauseRazedAndTheFlagsId()
    {
        var game = World();
        var flag = game.History.FlagId(1);
        var carriers = Enumerable
            .Range(0, game.Settlers.Count)
            .Where(slot => game.Settlers[slot].Activity is WaitsAtFlag)
            .ToList();
        var before = game.History.Events.Count;

        Do(game, CommandKind.RemoveFlag, 18, 6);

        var razed = Cause.Razed(flag);
        var made = game.History.Events.All.Skip(before).ToList();
        Assert.IsTrue(made.All(e => e.Cause == razed), Text(game));
        var own = game.History.EventsOf(flag).Skip(1).Select(e => e.Type).ToList();
        CollectionAssert.Contains(own, EventType.TookFlagDown);
        Assert.AreEqual(EventType.FreeFlagSlot, own[^1], "the flag's last event is the one that ends it");
        var settlers = made.Where(e => e.Thing.Kind == ThingId.SettlerKind).ToList();
        Assert.AreNotEqual(0, settlers.Count, "a settler of the flag's roads is touched:\n" + Text(game));
        Assert.IsTrue(
            settlers.All(e =>
                carriers.Contains(e.Thing.Slot) && e.Type is EventType.TakeWaiting or EventType.JoinRoads
            ),
            "only carriers waiting on a road (state 0x42) are touched, by take-waiting and join-roads:\n" + Text(game)
        );
    }

    [TestMethod]
    public void RazingAFlagWithTwoRoadsJoinsThemAndRecordsTheFlagsAtTheEndsNamingEachOther()
    {
        var game = World();
        var far = new[] { game.History.FlagId(0), game.History.FlagId(2) };
        var before = game.History.Events.Count;

        Do(game, CommandKind.RemoveFlag, 18, 6);

        foreach (var (id, other) in new[] { (far[0], far[1]), (far[1], far[0]) })
        {
            var join = game.History.EventsOf(id).Last(e => e.Type == EventType.JoinRoads);
            Assert.AreEqual(other.ToString(), join.Other);
            Assert.AreEqual(Cause.Razed(game.History.FlagId(1)), join.Cause);
        }

        var carriers = game
            .History.Events.All.Skip(before)
            .Where(e => e.Type == EventType.TakeWaiting)
            .Select(e => e.Thing)
            .ToList();
        Assert.AreNotEqual(0, carriers.Count, "the carriers of the two roads wait at the flag:\n" + Text(game));
        foreach (var carrier in carriers)
        {
            var join = game.History.EventsOf(carrier).Last(e => e.Type == EventType.JoinRoads);
            Assert.AreEqual($"{far[0]} {far[1]}", join.Other, carrier.ToString());
            Assert.AreEqual(Cause.Razed(game.History.FlagId(1)), join.Cause, carrier.ToString());
        }
    }

    [TestMethod]
    public void RazingABuildingRecordsRazedOnTheBuildingItsWorkerAndItsBurnAndFreeTicksLater()
    {
        var game = World();
        var hut = game.History.BuildingId(1);
        var worker = game.History.SettlerId(game.Buildings[1].Worker);

        Do(game, CommandKind.RemoveBuilding, 16, 2);
        var razedAt = game.TickCount - 1;
        for (var i = 0; i < 3000 && game.Buildings.Count > 1; i++)
        {
            game.Tick();
        }

        var razed = Cause.Razed(hut);
        // The window starts at the raze: the events before it (placement, worker request) are other behaviours.
        var events = game.History.EventsOf(hut).SkipWhile(e => e.Type != EventType.TookBuildingDown).ToList();
        Assert.AreEqual(EventType.TookBuildingDown, events[0].Type);
        Assert.IsTrue(events.All(e => e.Cause == razed), Text(game));
        Assert.IsTrue(events.Any(e => e.Type == EventType.Burn && e.Tick > razedAt));
        Assert.AreEqual(EventType.FreeBuilding, events[^1].Type);
        Assert.IsGreaterThan(razedAt, events[^1].Tick);
        var home = game.History.EventsOf(worker).Last(e => e.Type == EventType.SendWorkerHome);
        Assert.AreEqual(razed, home.Cause);
    }

    [TestMethod]
    public void AfterARazeTheStateOfTheBuildingAtEveryTickEqualsTheEnginesState()
    {
        var game = World();
        var hut = game.History.BuildingId(1);
        Do(game, CommandKind.RemoveBuilding, 16, 2);

        for (var i = 0; i < 3000 && game.Buildings.Count > 1; i++)
        {
            var (state, _) = game.History.StateAt(hut, game.TickCount - 1);
            var building = game.Buildings[1];
            Assert.AreEqual(BuildingEncoder.Status(building), state!.Get("status"), $"tick {game.TickCount - 1}");
            Assert.AreEqual(BuildingEncoder.Worker(building), state.Get("worker"), $"tick {game.TickCount - 1}");
            Assert.AreEqual((int)building.Stamp, state.Get("stamp"), $"tick {game.TickCount - 1}");
            game.Tick();
        }
    }

    static readonly string[] SettlerFields =
    [
        "job",
        "state",
        "player",
        "mode",
        "target",
        "next",
        "heading",
        "frame",
        "timer",
        "stamp",
        "x",
        "y",
    ];

    // Every settler and flag the raze touched is compared with the engine at every tick of the 60 after it: the movement and
    // carrier rules record what they do on later ticks (walking, waiting, the carriers of the joined roads arriving).
    [TestMethod]
    public void AfterARazeTheStateOfEachFlagAndSettlerTheRazeTouchedEqualsTheEnginesStateAndOfEachFlagAtEveryTickAfter()
    {
        var game = World();
        var before = game.History.Events.Count;

        Do(game, CommandKind.RemoveFlag, 18, 6);

        var razedFlag = game.History.FlagId(1);
        var touched = game
            .History.Events.All.Skip(before)
            .Select(e => e.Thing)
            .Distinct()
            .Where(t => t != razedFlag)
            .ToList();
        for (var i = 0; i < 60; i++)
        {
            var tick = game.TickCount - 1;
            foreach (var thing in touched)
            {
                var (state, refusal) = game.History.StateAt(thing, tick);
                Assert.IsNotNull(state, refusal);
                if (thing.Kind == ThingId.FlagKind)
                {
                    for (var direction = 0; direction < 6; direction++)
                    {
                        Assert.AreEqual(
                            FlagEncoder.RoadByte(game.Flags[thing.Slot].Side(direction)),
                            state.Get($"road{direction}"),
                            $"{thing} road{direction} tick {tick}"
                        );
                    }

                    Assert.AreEqual(
                        FlagEncoder.Status(game.Flags[thing.Slot]),
                        state.Get("status"),
                        $"{thing} status tick {tick}"
                    );
                }
                else
                {
                    var s = game.Settlers[thing.Slot];
                    var raw = SettlerEncoder.Fields(s);
                    int[] expected =
                    [
                        s.Job.Code,
                        raw.State,
                        s.Player,
                        raw.Mode,
                        raw.Target,
                        raw.Next,
                        raw.Heading,
                        s.Frame,
                        s.Timer,
                        raw.Stamp,
                        s.X,
                        s.Y,
                    ];
                    CollectionAssert.AreEqual(
                        expected,
                        SettlerFields.Select(f => state.Get(f)!.Value).ToArray(),
                        $"{thing} tick {tick}"
                    );
                }
            }

            game.Tick();
        }
    }
}
