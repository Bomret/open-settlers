using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The events the removal rules record (ADR 0011, docs/rules/removal.md), on the map of the seed 1234,5678,9ABC with the castle
// at 15,5: flags at 18,6 (slot 1), 19,4 (2), 17,3 (3), roads between them, a hut at 16,2.
[TestClass]
public sealed class ThingHistoryRemovalTests
{
    private static ProgramTables Made()
    {
        var classes = new byte[ProgramTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;

        return new ProgramTables(
            classes,
            Enumerable
                .Range(0, ProgramTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            Enumerable.Repeat((ushort)100, ProgramTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, ProgramTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, ProgramTables.ScanWindowCount).ToArray()
        );
    }

    private static Game NewGame()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var objects = map.Bytes(MapLayer.Object);
        for (var index = 0; index < objects.Length; index++)
        {
            if ((objects[index] & 0x7f) is >= 8 and < 0x18)
            {
                objects[index] &= 0x80;
            }
        }

        return new Game(Made(), map);
    }

    private static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    private static Game World()
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
        Do(game, CommandKind.PlaceBuilding, 16, 2);
        for (var i = 0; i < 400; i++)
        {
            game.Tick();
        }

        return game;
    }

    private static string Text(Game game) =>
        string.Join("\n", game.Events.All.Select(e => $"{e.Tick} {e.Type} {e.Thing} {e.Cause} {e.Other}"));

    [TestMethod]
    public void RazingAFlagWithARoadRecordsTheFlagTheCarrierAndTheFlagsEndWithCauseRazedAndTheFlagsId()
    {
        var game = World();
        var flag = game.FlagId(1);
        var carriers = Enumerable
            .Range(0, game.Settlers.Count)
            .Where(slot => game.Settlers[slot].State == 0x42)
            .ToList();
        var before = game.Events.Count;

        Do(game, CommandKind.RemoveFlag, 18, 6);

        var razed = Cause.Razed(flag);
        var made = game.Events.All.Skip(before).ToList();
        Assert.IsTrue(made.All(e => e.Cause == razed), Text(game));
        var own = game.EventsOf(flag).Skip(1).Select(e => e.Type).ToList();
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
        var far = new[] { game.FlagId(0), game.FlagId(2) };
        var before = game.Events.Count;

        Do(game, CommandKind.RemoveFlag, 18, 6);

        foreach (var (id, other) in new[] { (far[0], far[1]), (far[1], far[0]) })
        {
            var join = game.EventsOf(id).Last(e => e.Type == EventType.JoinRoads);
            Assert.AreEqual(other.ToString(), join.Other);
            Assert.AreEqual(Cause.Razed(game.FlagId(1)), join.Cause);
        }

        var carriers = game
            .Events.All.Skip(before)
            .Where(e => e.Type == EventType.TakeWaiting)
            .Select(e => e.Thing)
            .ToList();
        Assert.AreNotEqual(0, carriers.Count, "the carriers of the two roads wait at the flag:\n" + Text(game));
        foreach (var carrier in carriers)
        {
            var join = game.EventsOf(carrier).Last(e => e.Type == EventType.JoinRoads);
            Assert.AreEqual($"{far[0]} {far[1]}", join.Other, carrier.ToString());
            Assert.AreEqual(Cause.Razed(game.FlagId(1)), join.Cause, carrier.ToString());
        }
    }

    [TestMethod]
    public void RazingABuildingRecordsRazedOnTheBuildingItsWorkerAndItsBurnAndFreeTicksLater()
    {
        var game = World();
        var hut = game.BuildingId(1);
        var worker = game.SettlerId(game.Buildings[1].Worker);

        Do(game, CommandKind.RemoveBuilding, 16, 2);
        var razedAt = game.TickCount - 1;
        for (var i = 0; i < 3000 && game.Buildings.Count > 1; i++)
        {
            game.Tick();
        }

        var razed = Cause.Razed(hut);
        // The window starts at the raze: the events before it (placement, worker request) are other behaviours.
        var events = game.EventsOf(hut).SkipWhile(e => e.Type != EventType.TookBuildingDown).ToList();
        Assert.AreEqual(EventType.TookBuildingDown, events[0].Type);
        Assert.IsTrue(events.All(e => e.Cause == razed), Text(game));
        Assert.IsTrue(events.Any(e => e.Type == EventType.Burn && e.Tick > razedAt));
        Assert.AreEqual(EventType.FreeBuilding, events[^1].Type);
        Assert.IsGreaterThan(razedAt, events[^1].Tick);
        var home = game.EventsOf(worker).Last(e => e.Type == EventType.SendWorkerHome);
        Assert.AreEqual(razed, home.Cause);
    }

    [TestMethod]
    public void AfterARazeTheStateOfTheBuildingAtEveryTickEqualsTheEnginesState()
    {
        var game = World();
        var hut = game.BuildingId(1);
        Do(game, CommandKind.RemoveBuilding, 16, 2);

        for (var i = 0; i < 3000 && game.Buildings.Count > 1; i++)
        {
            var (state, _) = game.StateAt(hut, game.TickCount - 1);
            var building = game.Buildings[1];
            Assert.AreEqual(building.Status, state!.Get("status"), $"tick {game.TickCount - 1}");
            Assert.AreEqual((int)building.Worker, state.Get("worker"), $"tick {game.TickCount - 1}");
            Assert.AreEqual((int)building.Stamp, state.Get("stamp"), $"tick {game.TickCount - 1}");
            game.Tick();
        }
    }

    private static readonly string[] SettlerFields =
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
        var before = game.Events.Count;

        Do(game, CommandKind.RemoveFlag, 18, 6);

        var razedFlag = game.FlagId(1);
        var touched = game.Events.All.Skip(before).Select(e => e.Thing).Distinct().Where(t => t != razedFlag).ToList();
        for (var i = 0; i < 60; i++)
        {
            var tick = game.TickCount - 1;
            foreach (var thing in touched)
            {
                var (state, refusal) = game.StateAt(thing, tick);
                Assert.IsNotNull(state, refusal);
                if (thing.Kind == ThingId.FlagKind)
                {
                    for (var direction = 0; direction < 6; direction++)
                    {
                        Assert.AreEqual(
                            game.RoadByte(thing.Slot, direction),
                            state.Get($"road{direction}"),
                            $"{thing} road{direction} tick {tick}"
                        );
                    }

                    Assert.AreEqual(game.FlagStatus(thing.Slot), state.Get("status"), $"{thing} status tick {tick}");
                }
                else
                {
                    var s = game.Settlers[thing.Slot];
                    int[] expected =
                    [
                        s.Job,
                        s.State,
                        s.Player,
                        s.Mode,
                        s.Target,
                        s.Next,
                        s.Heading,
                        s.Frame,
                        s.Timer,
                        s.Stamp,
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
