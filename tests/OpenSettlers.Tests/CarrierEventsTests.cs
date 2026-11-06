using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The events of the carrier rules (ADR 0011) on the map of the seed 1234,5678,9ABC with the castle at 15,5: a road from the
// castle's flag at 16,6 to a flag at 18,6, placed by a command.
[TestClass]
public sealed class CarrierEventsTests
{
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

        var classes = new byte[ProgramTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;

        return new Game(
            new ProgramTables(
                classes,
                Enumerable
                    .Range(0, ProgramTables.StartStockRows)
                    .Select(row =>
                        Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray()
                    )
                    .ToArray(),
                Enumerable.Repeat((ushort)100, ProgramTables.StepTimeCount).ToArray(),
                Enumerable.Repeat((byte)8, ProgramTables.DoorSpeedCount).ToArray(),
                Enumerable.Repeat((ushort)16, ProgramTables.ScanWindowCount).ToArray()
            ),
            map
        );
    }

    private static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    // Runs the game until a settler waits at a flag on the road, with the road placed by a command.
    private static Game RoadWithACarrier()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        for (var i = 0; i < 400 && !game.Events.All.Any(e => e.Type == EventType.Wait); i++)
        {
            game.Tick();
        }

        return game;
    }

    [TestMethod]
    public void ARequestedCarrierRecordsTheRequestNamingTheFlagWithTheCauseOfTheRoadsPlacement()
    {
        var game = RoadWithACarrier();

        var request = game.Events.All.First(e => e.Type == EventType.CarrierRequested);

        Assert.AreEqual(ThingId.SettlerKind, request.Thing.Kind);
        Assert.AreEqual(Cause.Placed(game.FlagId(0)), request.Cause);
        Assert.AreEqual(game.FlagId((int)request.State.Get("target")!.Value).ToString(), request.Other);
        Assert.AreEqual(Settler.CarrierJob, request.State.Get("job"));
    }

    [TestMethod]
    public void TheCarrierEntersItsRoadAndWaitsWithTheCauseOfThePlacement()
    {
        var game = RoadWithACarrier();
        var carrier = game.Events.All.First(e => e.Type == EventType.CarrierRequested).Thing;

        var events = game.EventsOf(carrier).SkipWhile(e => e.Type != EventType.CarrierRequested).ToList();

        Assert.IsTrue(events.Any(e => e.Type == EventType.EnterRoad), "the carrier enters the road");
        Assert.IsTrue(events.All(e => e.Cause == Cause.Placed(game.FlagId(0))), "the cause is carried up to the wait");
        Assert.AreEqual(EventType.Wait, events[^1].Type);
        Assert.AreEqual(0x42, events[^1].State.Get("state"));
    }

    [TestMethod]
    public void AWaitingCarrierAfterNoCommandRecordsTheWaitWithTheCauseCarriers()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        game.InLayout = true;
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        game.InLayout = false;
        for (var i = 0; i < 400 && !game.Events.All.Any(e => e.Type == EventType.Wait); i++)
        {
            game.Tick();
        }

        var wait = game.Events.All.First(e => e.Type == EventType.Wait);

        Assert.AreEqual(Cause.Carriers, wait.Cause);
    }

    [TestMethod]
    public void TheStateOfTheCarrierAtTheTicksOfItsEventsEqualsTheEnginesStateThen()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        var wait = (ThingEvent?)null;
        for (var i = 0; i < 400 && wait is null; i++)
        {
            game.Tick();
            wait = game.Events.All.FirstOrDefault(e => e.Type == EventType.Wait);
        }

        var carrier = wait!.Thing;
        var settler = game.Settlers[carrier.Slot];
        var (state, _) = game.StateAt(carrier, game.TickCount - 1);

        Assert.AreEqual(settler.State, state!.Get("state"));
        Assert.AreEqual((int)settler.Timer, state.Get("timer"));
        Assert.AreEqual(settler.Heading, state.Get("heading"));
        Assert.AreEqual(settler.Frame, state.Get("frame"));
    }

    [TestMethod]
    public void TheFlagsOfTheRoadRecordTheRequestAndTheEntryOfTheCarrier()
    {
        var game = RoadWithACarrier();

        var types = game.EventsOf(game.FlagId(1)).Select(e => e.Type).ToList();

        Assert.Contains(EventType.CarrierRequested, types);
        Assert.Contains(EventType.EnterRoad, types);
    }
}
