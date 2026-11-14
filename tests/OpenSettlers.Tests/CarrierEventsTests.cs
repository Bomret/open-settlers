using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The events of the carrier rules (ADR 0011) on the map of the seed 1234,5678,9ABC with the castle at 15,5: a road from the
// castle's flag at 16,6 to a flag at 18,6, placed by a command.
[TestClass]
public sealed class CarrierEventsTests
{
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

        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        classes[2] = 4;

        return GameFactory.Create(
            new RuleTables(
                classes,
                Enumerable
                    .Range(0, RuleTables.StartStockRows)
                    .Select(row =>
                        Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray()
                    )
                    .ToArray(),
                Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount).ToArray(),
                Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount).ToArray(),
                Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount).ToArray()
            ),
            map
        );
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

    static Game RoadBuilt()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);

        return game;
    }

    // Runs the game until the castle has sent a carrier to the road, which is then on its way.
    static void RunToTheRequest(Game game)
    {
        for (var i = 0; i < 400 && !game.History.Events.All.Any(e => e.Type == EventType.CarrierRequested); i++)
        {
            game.Tick();
        }
    }

    static void RunUntil(Game game, System.Func<bool> done)
    {
        for (var i = 0; i < 3000 && !done(); i++)
        {
            game.Tick();
        }

        Assert.IsTrue(done(), "the condition came true");
    }

    static void Run(Game game, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            game.Tick();
        }
    }

    // Runs the game until a settler waits at a flag on the road, with the road placed by a command.
    static Game RoadWithACarrier()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        for (var i = 0; i < 400 && !game.History.Events.All.Any(e => e.Type == EventType.Wait); i++)
        {
            game.Tick();
        }

        return game;
    }

    [TestMethod]
    public void ARequestedCarrierRecordsTheRequestNamingTheFlagWithTheCauseOfTheRoadsPlacement()
    {
        var game = RoadWithACarrier();

        var request = game.History.Events.All.First(e => e.Type == EventType.CarrierRequested);

        Assert.AreEqual(ThingId.SettlerKind, request.Thing.Kind);
        Assert.AreEqual(Cause.Placed(game.History.FlagId(0)), request.Cause);
        Assert.AreEqual(game.History.FlagId((int)request.State.Get("target")!.Value).ToString(), request.Other);
        Assert.AreEqual(Carrier.Number, request.State.Get("job"));
    }

    [TestMethod]
    public void TheCarrierEntersItsRoadAndWaitsWithTheCauseOfThePlacement()
    {
        var game = RoadWithACarrier();
        var carrier = game.History.Events.All.First(e => e.Type == EventType.CarrierRequested).Thing;

        // The window runs from the request to the first wait: the turn's `walked` and later goods work are other behaviours.
        var history = game.History.EventsOf(carrier).SkipWhile(e => e.Type != EventType.CarrierRequested).ToList();
        var events = history.Take(history.FindIndex(e => e.Type == EventType.Wait) + 1).ToList();

        Assert.IsTrue(events.Any(e => e.Type == EventType.EnterRoad), "the carrier enters the road");
        Assert.IsTrue(
            events.All(e => e.Cause == Cause.Placed(game.History.FlagId(0))),
            "the cause is carried up to the wait"
        );
        Assert.AreEqual(EventType.Wait, events[^1].Type);
        Assert.AreEqual(0x42, events[^1].State.Get("state"));
    }

    [TestMethod]
    public void TheCarriedPlacedCauseEndsAtTheCarriersFirstWait()
    {
        var game = RoadWithACarrier();
        var carrier = game.History.Events.All.First(e => e.Type == EventType.CarrierRequested).Thing;

        var history = game.History.EventsOf(carrier).ToList();
        var after = history[history.FindIndex(e => e.Type == EventType.Wait) + 1];

        Assert.AreEqual(Cause.Settlers, after.Cause, after.ToString());
    }

    [TestMethod]
    public void TheEventStoreGrowsNoFasterThanLinearlyOverALongGame()
    {
        var game = RoadWithACarrier();
        const int ticks = 2000;
        var start = game.History.Events.Count;
        for (var i = 0; i < ticks; i++)
        {
            game.Tick();
        }

        var first = game.History.Events.Count - start;
        for (var i = 0; i < ticks; i++)
        {
            game.Tick();
        }

        var both = game.History.Events.Count - start;
        Assert.IsLessThanOrEqualTo(first * 5 / 2, both, $"first {first}, both {both}");
    }

    [TestMethod]
    public void AWaitingCarrierAfterNoCommandRecordsTheWaitWithTheCauseCarriers()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        game.Causes.InLayout = true;
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        game.Causes.InLayout = false;
        for (var i = 0; i < 400 && !game.History.Events.All.Any(e => e.Type == EventType.Wait); i++)
        {
            game.Tick();
        }

        var wait = game.History.Events.All.First(e => e.Type == EventType.Wait);

        Assert.AreEqual(Cause.Carriers, wait.Cause);
    }

    // The state of the carrier at every tick from its request to its wait, against the engine's, in every field. The settler's
    // generic stepping (state 0xf to 5 to 2: Game.Settlers) records its changes between the carrier rules' events.
    [TestMethod]
    public void TheStateOfTheCarrierAtEveryTickOfItsWalkEqualsTheEnginesStateThen()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);
        var compared = 0;
        var eventTicks = 0;
        for (var i = 0; i < 400; i++)
        {
            game.Tick();
            if (game.History.Events.All.FirstOrDefault(e => e.Type == EventType.CarrierRequested) is not { } request)
            {
                continue;
            }

            var tick = game.TickCount - 1;
            var settler = game.Settlers[request.Thing.Slot];
            var (state, _) = game.History.StateAt(request.Thing, tick);
            var raw = SettlerEncoder.Fields(settler);
            compared++;
            Assert.AreEqual(settler.Job.Code, state!.Get("job"), $"job at tick {tick}");
            Assert.AreEqual(raw.Mode, state.Get("mode"), $"mode at tick {tick}");
            Assert.AreEqual(raw.Target, state.Get("target"), $"target at tick {tick}");
            if (game.History.EventsOf(request.Thing).Any(e => e.Tick == tick))
            {
                eventTicks++;
            }

            Assert.AreEqual(raw.State, state.Get("state"), $"state at tick {tick}");
            Assert.AreEqual(raw.Next, state.Get("next"), $"next at tick {tick}");
            Assert.AreEqual((int)settler.Timer, state.Get("timer"), $"timer at tick {tick}");
            Assert.AreEqual(raw.Heading, state.Get("heading"), $"heading at tick {tick}");
            Assert.AreEqual(settler.Frame, state.Get("frame"), $"frame at tick {tick}");
            Assert.AreEqual((settler.X, settler.Y), (state.Get("x"), state.Get("y")), $"node at tick {tick}");

            if (settler.Activity is WaitsAtFlag)
            {
                break;
            }
        }

        Assert.IsGreaterThan(30, compared, "every tick of the walk was compared");
        Assert.IsGreaterThanOrEqualTo(2, eventTicks, "the request and the wait");
    }

    [TestMethod]
    public void TheFlagsOfTheRoadRecordTheRequestAndTheEntryOfTheCarrier()
    {
        var game = RoadWithACarrier();

        var types = game.History.EventsOf(game.History.FlagId(1)).Select(e => e.Type).ToList();

        Assert.Contains(EventType.CarrierRequested, types);
        Assert.Contains(EventType.EnterRoad, types);
    }

    [TestMethod]
    public void TheFlagsOfTheRequestCarryTheCauseOfTheRoadsPlacementAsTheCarrierDoes()
    {
        var game = RoadWithACarrier();

        var requests = new[] { game.History.FlagId(0), game.History.FlagId(1) }
            .Select(flag => game.History.EventsOf(flag).First(e => e.Type == EventType.CarrierRequested))
            .ToList();

        Assert.IsTrue(requests.All(e => e.Cause == Cause.Placed(game.History.FlagId(0))));
    }

    [TestMethod]
    public void ALaterRequestOnTheSameRoadCarriesTheCauseCarriers()
    {
        var game = RoadWithACarrier();
        // The carrier is gone from the road: both ends count none, and the castle holds another settler to send.
        foreach (var (flag, direction) in new[] { (0, 0), (1, 3) })
        {
            game.Flags[flag].Side(direction).Carriers = 0;
        }

        Run(game, 400);

        var requests = game.History.Events.All.Where(e =>
            e.Type == EventType.CarrierRequested && e.Thing.Kind == ThingId.SettlerKind
        );
        Assert.AreEqual(2, requests.Count(), "the road asked again");
        Assert.AreEqual(Cause.Carriers, requests.Last().Cause);
    }

    [TestMethod]
    public void AFlagTurnThatChangesTheStatusRecordsVisitFlagWithTheCauseCarriers()
    {
        var game = RoadWithACarrier();
        Run(game, 40);

        var visit = game.History.EventsOf(game.History.FlagId(0)).First(e => e.Type == EventType.VisitFlag);

        Assert.AreEqual(Cause.Carriers, visit.Cause);
        Assert.AreEqual(1, visit.State.Get("status")!.Value & 1, "the road in direction 0 holds a carrier");
    }

    [TestMethod]
    public void TheRetryScanRecordsClearFlagRetryBitsForTheFlagWhoseRequestFoundNoCarrier()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Do(game, CommandKind.PlaceFlag, 19, 4);
        Do(game, CommandKind.BuildRoad, 18, 6, [5, 5, 0]);
        Run(game, 40);

        var types = game.History.EventsOf(game.History.FlagId(1)).ToList();
        var cleared = types.First(e => e.Type == EventType.ClearFlagRetryBits);

        Assert.AreEqual(Cause.Carriers, cleared.Cause);
        Assert.AreEqual(0, cleared.State.Get("status")!.Value & 0x80);
        Assert.IsTrue(
            types.Any(e => e.Type == EventType.VisitFlag && (e.State.Get("status")!.Value & 0x80) != 0),
            "the failed request marked the flag first"
        );
    }

    [TestMethod]
    public void ACarrierThatMeetsNoWayOnOnForItsRoadRecordsWalkRoad()
    {
        var game = RoadBuilt();
        game.Map.At(17, 6).Roads = RoadSet.Empty;
        Run(game, 400);

        var carrier = game.History.Events.All.First(e => e.Type == EventType.CarrierRequested).Thing;
        var walk = game.History.EventsOf(carrier).First(e => e.Type == EventType.WalkRoad);

        Assert.AreEqual(Cause.Placed(game.History.FlagId(0)), walk.Cause);
        Assert.AreEqual(0, walk.State.Get("timer"));
        Assert.AreEqual(3, walk.State.Get("state"));
    }

    static void RunToTheTurn(Game game, ThingId carrier, int turn) =>
        RunUntil(game, () => game.History.EventsOf(carrier).Count(e => e.Type == EventType.Wait) == turn);

    // No command gives a road a second carrier in these tests' short setups, so a test sets the count it needs.
    static void SetRoadCount(Game game, int count)
    {
        foreach (var (flag, direction) in new[] { (0, 0), (1, 3) })
        {
            game.Flags[flag].Side(direction).Carriers = count;
        }
    }

    [TestMethod]
    public void ACarrierOnARoadOfTwoCarriersLeavesItAtItsFourthTurnAndFlagsRecordIt()
    {
        var game = RoadBuilt();
        RunToTheRequest(game);
        SetRoadCount(game, 2);
        var carrier = game.History.Events.All.First(e => e.Type == EventType.CarrierRequested).Thing;

        for (var turn = 1; turn <= 4; turn++)
        {
            RunToTheTurn(game, carrier, turn);
        }

        var waits = game.History.EventsOf(carrier).Where(e => e.Type == EventType.Wait).ToList();
        Assert.AreEqual(3, waits[2].State.Get("next"), "three turns counted");
        Assert.AreEqual(0xff, waits[3].State.Get("next"), "sent away at the fourth");
        Assert.IsTrue(waits.All(e => e.Cause == Cause.Placed(game.History.FlagId(0))));
        var flagWait = game.History.EventsOf(game.History.FlagId(1)).Single(e => e.Type == EventType.Wait);
        Assert.AreEqual(carrier.ToString(), flagWait.Other);
        Assert.AreEqual(Cause.Carriers, flagWait.Cause);
        Assert.AreEqual(2, flagWait.State.Get("road3")!.Value & 0xf, "the road counts one carrier less");
        Assert.AreEqual(1, game.History.EventsOf(game.History.FlagId(0)).Count(e => e.Type == EventType.Wait));
    }

    [TestMethod]
    public void ACarrierOnARoadOfTwoCarriersStepsOnAfterEachTurnAtTheEndOfItsRoad()
    {
        var game = RoadBuilt();
        RunToTheRequest(game);
        SetRoadCount(game, 2);
        var carrier = game.History.Events.All.First(e => e.Type == EventType.CarrierRequested).Thing;

        RunUntil(game, () => game.History.EventsOf(carrier).Count(e => e.Type == EventType.Wait) == 3);

        var events = game.History.EventsOf(carrier).ToList();
        var waits = events.Select((e, i) => (e, i)).Where(p => p.e.Type == EventType.Wait).Select(p => p.i).ToList();
        for (var turn = 1; turn < waits.Count; turn++)
        {
            Assert.IsTrue(
                events
                    .Skip(waits[turn - 1] + 1)
                    .Take(waits[turn] - waits[turn - 1] - 1)
                    .Any(e => e.Type == EventType.Stepped),
                $"the carrier stepped between turn {turn} and turn {turn + 1}"
            );
        }
    }

    [TestMethod]
    public void ACarrierWaitsOnWhileAGoodWaitsAtTheFlagAtTheFarEndOfItsRoadAndLeavesItWhenTheGoodIsGone()
    {
        // A road of four steps from the castle's flag to a flag at 20,6. A turn at the end of the road sends the carrier back the
        // way it came, to its next turn next to the flag at the other end: the first is next to the flag at 20,6, the second next
        // to the castle's flag, whose far end is the flag at 20,6.
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);
        Do(game, CommandKind.PlaceFlag, 20, 6);
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0, 0, 0]);
        RunToTheRequest(game);
        SetRoadCount(game, 2);
        var carrier = game.History.Events.All.First(e => e.Type == EventType.CarrierRequested).Thing;
        RunToTheTurn(game, carrier, 1);
        // The road holds one carrier again, and a good waits at the flag at 20,6 for it.
        SetRoadCount(game, 1);
        game.Flags[1].Side(3).WaitingPlace = 0;

        RunToTheTurn(game, carrier, 2);

        Assert.AreNotEqual(0x42, game.History.EventsOf(carrier).Last().State.Get("state"), "the carrier waits on");

        game.Flags[1].Side(3).WaitingPlace = null;
        RunToTheTurn(game, carrier, 3);

        Assert.AreEqual(0x42, game.History.EventsOf(carrier).Last().State.Get("state"), "it waits at the flag");
    }
}
