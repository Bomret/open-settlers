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
        var classes = new byte[ProgramTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;
        var tables = new ProgramTables(
            classes,
            Enumerable
                .Range(0, ProgramTables.StartStockRows)
                .Select(row => Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray())
                .ToArray(),
            Enumerable.Repeat((ushort)100, ProgramTables.StepTimeCount).ToArray(),
            Enumerable.Repeat((byte)8, ProgramTables.DoorSpeedCount).ToArray(),
            Enumerable.Repeat((ushort)16, ProgramTables.ScanWindowCount).ToArray()
        );
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var objects = map.Bytes(MapLayer.Object);
        for (var index = 0; index < objects.Length; index++)
        {
            if ((objects[index] & 0x7f) is >= 8 and < 0x18)
            {
                objects[index] &= 0x80;
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
        game.Settlers.Select((s, slot) => (s, slot)).First(p => p.s.Job == Settler.WoodcutterJob).slot;

    [TestMethod]
    public void AWorkerRequestedForAHutRecordsTheBuildingAndEachStepOfItsWalkTheNode()
    {
        var game = Built(out var hut);
        var hutId = game.BuildingId(hut);
        var worker = game.SettlerId(WoodcutterSlot(game));
        for (var i = 0; i < 400 && game.Settlers[WoodcutterSlot(game)].State != 4; i++)
        {
            game.Tick();
        }

        var events = game.EventsOf(worker);
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
            game.EventsOf(hutId).Any(e => e.Type == EventType.WorkerRequested && e.Other == worker.ToString())
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

        var castle = game.BuildingId(0);
        var changes = game.EventsOf(castle).Where(e => e.Type == EventType.Builder).ToList();
        Assert.IsNotEmpty(changes);
        Assert.AreEqual("settlers", changes[0].Cause.ToString());
        Assert.AreEqual(game.Buildings[0].Progress, changes[^1].State.Get("progress"));
        Assert.IsTrue(
            game.EventsOf(game.SettlerId(0)).Any(e => e.Type == EventType.Builder && e.Other == castle.ToString())
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
        for (var i = 0; i < 400 && game.Settlers[slot].State != 4; i++)
        {
            game.Tick();
            for (var s = 0; s < game.Settlers.Count; s++)
            {
                var settler = game.Settlers[s];
                if (settler.Job == Settler.CarrierJob)
                {
                    // The carriers' rules record their own changes.
                    continue;
                }

                int[] expected =
                [
                    settler.Job,
                    settler.State,
                    settler.Player,
                    settler.Mode,
                    settler.Target,
                    settler.Next,
                    settler.Heading,
                    settler.Frame,
                    settler.Timer,
                    settler.Stamp,
                    settler.X,
                    settler.Y,
                ];
                var (state, refusal) = game.StateAt(game.SettlerId(s), game.TickCount - 1);
                Assert.IsNotNull(state, refusal);
                CollectionAssert.AreEqual(
                    expected,
                    fields.Select(f => state.Get(f)!.Value).ToArray(),
                    $"settler {s} at tick {game.TickCount - 1}"
                );
            }
        }

        Assert.AreEqual(4, game.Settlers[slot].State);
    }
}
