using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The events of the settler rules' main pass (ADR 0011): the request for a worker, its walk, and the castle's builder, on the map
// of the seed 1234,5678,9ABC with the castle at 15,5 and a hut at 16,2 whose flag stands at 17,3.
[TestClass]
public sealed class ThingHistorySettlersTests
{
    private static Game NewGame()
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;
        var tables = new RuleTables(
            classes,
            Enumerable
                .Range(0, RuleTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount).ToArray()
        );
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        return new Game(tables, map);
    }

    private static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    private static Game Built(out int hutSlot)
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
        hutSlot = 1;

        return game;
    }

    private static int WoodcutterSlot(Game game) =>
        game.Settlers.Select((s, slot) => (s, slot)).First(p => p.s.Job == Job.Woodcutter).slot;

    [TestMethod]
    public void AWorkerRequestedForAHutRecordsTheBuildingAndEachStepOfItsWalkTheNode()
    {
        var game = Built(out var hut);
        var hutId = game.History.BuildingId(hut);
        var worker = game.History.SettlerId(WoodcutterSlot(game));
        for (var i = 0; i < 400 && game.Settlers[WoodcutterSlot(game)].Activity is not Door; i++)
        {
            game.Tick();
        }

        var events = game.History.EventsOf(worker);
        var request = events.Single(e => e.Type == EventType.WorkerRequested);
        Assert.AreEqual(hutId.ToString(), request.Other);
        Assert.AreEqual($"placed {hutId}", request.Cause.ToString());
        var steps = events.Where(e => e.Type == EventType.Stepped).ToList();
        Assert.IsNotEmpty(steps);
        Assert.IsTrue(steps.All(e => e.State.Get("x") is not null && e.Cause.Equals(request.Cause)));
        Assert.AreNotEqual(
            steps[0].State.Get("x") * 1000 + steps[0].State.Get("y"),
            steps[^1].State.Get("x") * 1000 + steps[^1].State.Get("y")
        );
        Assert.IsTrue(
            game.History.EventsOf(hutId).Any(e => e.Type == EventType.WorkerRequested && e.Other == worker.ToString())
        );
    }

    [TestMethod]
    public void TheBuilderRecordsItsChangesWithTheCastlesId()
    {
        var game = Built(out _);
        for (var i = 0; i < 40; i++)
        {
            game.Tick();
        }

        var castle = game.History.BuildingId(0);
        var changes = game.History.EventsOf(castle).Where(e => e.Type == EventType.Builder).ToList();
        Assert.IsNotEmpty(changes);
        Assert.AreEqual("settlers", changes[0].Cause.ToString());
        Assert.AreEqual(game.Buildings[0].Progress, changes[^1].State.Get("progress"));
        Assert.IsTrue(
            game.History.EventsOf(game.History.SettlerId(0))
                .Any(e => e.Type == EventType.Builder && e.Other == castle.ToString())
        );
    }

    [TestMethod]
    public void ASettlersStateAtEveryTickOfAWalkEqualsTheEnginesState()
    {
        var game = Built(out _);
        var slot = WoodcutterSlot(game);
        string[] fields =
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
        for (var i = 0; i < 400 && game.Settlers[slot].Activity is not Door; i++)
        {
            game.Tick();
            for (var s = 0; s < game.Settlers.Count; s++)
            {
                var settler = game.Settlers[s];
                var raw = SettlerEncoder.Fields(settler);
                if (settler.Job == Job.Carrier)
                {
                    // The carriers' rules record their own changes.
                    continue;
                }

                int[] expected =
                [
                    settler.Job.Code,
                    raw.State,
                    settler.Player,
                    raw.Mode,
                    raw.Target,
                    raw.Next,
                    raw.Heading,
                    settler.Frame,
                    settler.Timer,
                    raw.Stamp,
                    settler.X,
                    settler.Y,
                ];
                var (state, refusal) = game.History.StateAt(game.History.SettlerId(s), game.TickCount - 1);
                Assert.IsNotNull(state, refusal);
                CollectionAssert.AreEqual(
                    expected,
                    fields.Select(f => state.Get(f)!.Value).ToArray(),
                    $"settler {s} at tick {game.TickCount - 1}"
                );
            }
        }

        Assert.IsInstanceOfType<Door>(game.Settlers[slot].Activity);
    }

    [TestMethod]
    public void AHutWhoseRequestFindsNoWorkerRecordsBuildingHandledAndLaterRetryBitsCleared()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 17, 3);
        Do(game, CommandKind.PlaceBuilding, 16, 2);
        var hut = game.History.BuildingId(1);
        for (var i = 0; i < 400; i++)
        {
            game.Tick();
        }

        var events = game.History.EventsOf(hut);
        var handled = events.First(e => e.Type == EventType.BuildingHandled);
        Assert.AreEqual(4, handled.State.Get("status")! & 4, "the unanswered request sets bit 2");
        Assert.AreEqual($"placed {hut}", handled.Cause.ToString());
        var cleared = events.First(e => e.Type == EventType.RetryBitsCleared);
        Assert.AreEqual(0, cleared.State.Get("status")! & 4, "the scan clears bit 2");
        Assert.IsGreaterThan(handled.Tick, cleared.Tick);
    }

    [TestMethod]
    public void AHutsStateAtEveryTickOfTheWorkersWalkEqualsTheEnginesState()
    {
        var game = Built(out var hutSlot);
        var hut = game.History.BuildingId(hutSlot);
        for (var i = 0; i < 400 && game.Settlers[WoodcutterSlot(game)].Activity is not Door; i++)
        {
            game.Tick();
            var b = game.Buildings[hutSlot];
            int[] expected =
            [
                b.Type.Code,
                b.Constructing ? 1 : 0,
                b.X,
                b.Y,
                b.Flag,
                BuildingEncoder.Status(b),
                BuildingEncoder.Worker(b),
                b.Stamp,
                BuildingEncoder.Fill(b),
                b.Progress,
            ];
            var (state, refusal) = game.History.StateAt(hut, game.TickCount - 1);
            Assert.IsNotNull(state, refusal);
            CollectionAssert.AreEqual(
                expected,
                new[] { "type", "constructing", "x", "y", "flag", "status", "worker", "stamp", "fill", "progress" }
                    .Select(f => state.Get(f)!.Value)
                    .ToArray(),
                $"hut at tick {game.TickCount - 1}"
            );
        }
    }

    [TestMethod]
    public void AKnightWaitingInTheCastleRecordsKnightIdledWithTheSettlersCause()
    {
        var game = Built(out _);
        for (var i = 0; i < 400; i++)
        {
            game.Tick();
        }

        var idled = game.History.Events.All.Where(e => e.Type == EventType.KnightIdled).ToList();
        Assert.IsNotEmpty(idled);
        Assert.IsTrue(idled.All(e => e.Cause == Cause.Settlers && e.State.Get("job") >= 22));
    }

    [TestMethod]
    public void AWorkerWhoFindsNoRoadOnTurnsAwayAndTheHutRecordsIt()
    {
        var game = Built(out var hutSlot);
        var worker = WoodcutterSlot(game);
        for (var i = 0; i < 400 && game.Settlers[worker].Activity is not Walks; i++)
        {
            game.Tick();
        }

        Assert.IsInstanceOfType<Walks>(game.Settlers[worker].Activity, "the worker walks the roads");
        // The last road to the hut's flag (from 17,3 east to 18,3 and south-east to 19,4).
        Do(game, CommandKind.RemoveRoad, 18, 3);
        for (var i = 0; i < 600; i++)
        {
            game.Tick();
        }

        var hut = game.History.BuildingId(hutSlot);
        var turned = game.History.EventsOf(hut).Single(e => e.Type == EventType.Walked);
        Assert.AreEqual(game.History.SettlerId(worker).ToString(), turned.Other);
        Assert.AreEqual(0, turned.State.Get("status")! & 0x80, "the worker on the way ends");
    }
}
