using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;
using static OpenSettlers.Adapters.Testing.HistoryWorld;
using static OpenSettlers.Tests.LaidGame;

namespace OpenSettlers.Tests;

// Thing IDs, the event store, causes, and the list file's ids (ADR 0011), on the map of the seed 1234,5678,9ABC with the
// castle at 15,5 (its flag at 16,6).
[TestClass]
public sealed class ThingHistoryTests
{
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

    // The world of GameThingTests: flags at 18,6 (slot 1), 19,4 (2), 17,3 (3), roads between them, a hut at 16,2.
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
        for (var i = 0; i < 60; i++)
        {
            game.Tick();
        }

        return game;
    }

    [TestMethod]
    public void TheDigestOfAFixedGameIsTheOneTakenBeforeThingHistory()
    {
        // The digest takes each flag's two request words (FlagEncoder.Requests) and the object pass's decay counter; the value was
        // regenerated for them.
        Assert.AreEqual(15019566634781816344UL, World().Digest());
    }

    static Game Castle()
    {
        var game = NewGame();
        Do(game, CommandKind.PlaceCastle, 15, 5);

        return game;
    }

    [TestMethod]
    public void AFlagSlotFilledAgainGivesTheNewFlagTheNextFillCount()
    {
        var game = Castle();
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Do(game, CommandKind.PlaceFlag, 19, 4);

        Assert.AreEqual("flag#2.1", game.History.FlagId(2).ToString());

        // The last slot: the list is trimmed at its end, and the count stays.
        Do(game, CommandKind.RemoveFlag, 19, 4);
        Assert.HasCount(2, game.Flags);
        Do(game, CommandKind.PlaceFlag, 19, 4);
        Assert.AreEqual("flag#2.2", game.History.FlagId(2).ToString());

        // A slot in the middle of the list.
        Do(game, CommandKind.RemoveFlag, 18, 6);
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Assert.AreEqual("flag#1.2", game.History.FlagId(1).ToString());
        Assert.AreEqual("flag#2.2", game.History.FlagId(2).ToString());
    }

    [TestMethod]
    public void ABuildingSlotFilledAgainAfterTheListWasTrimmedGivesTheNextFillCount()
    {
        var game = Castle();
        Do(game, CommandKind.PlaceFlag, 17, 3);
        Huts.PlaceAndTick(game, 16, 2);
        Assert.AreEqual("building#0.1", game.History.BuildingId(0).ToString());
        Assert.AreEqual("building#1.1", game.History.BuildingId(1).ToString());

        Do(game, CommandKind.RemoveBuilding, 16, 2);
        for (var i = 0; i < 3000 && game.Buildings.Count > 1; i++)
        {
            game.Tick();
        }

        Assert.HasCount(1, game.Buildings);
        Do(game, CommandKind.PlaceFlag, 17, 3);
        Huts.PlaceAndTick(game, 16, 2);
        Assert.AreEqual("building#1.2", game.History.BuildingId(1).ToString());
    }

    [TestMethod]
    public void TheCastleSettlersTakeTheFirstFillOfTheirSlots()
    {
        var game = Castle();

        Assert.AreEqual("settler#0.1", game.History.SettlerId(0).ToString());
        Assert.AreEqual("settler#19.1", game.History.SettlerId(19).ToString());
    }

    [TestMethod]
    public void PlacingTheCastleRecordsTheEventsOfTheCastleItsFlagAndItsSettlers()
    {
        var game = Castle();
        var castle = game.History.BuildingId(0);

        var events = game.History.EventsOf(castle);

        Assert.HasCount(1, events);
        Assert.AreEqual(EventType.PlaceCastle, events[0].Type);
        Assert.AreEqual(0L, events[0].Tick);
        Assert.AreEqual(Cause.Placed(castle), events[0].Cause);
        Assert.AreEqual(game.History.FlagId(0).ToString(), events[0].Other);
        Assert.AreEqual(15, events[0].State.Get("x"));
        Assert.AreEqual(Cause.Placed(castle), game.History.EventsOf(game.History.FlagId(0)).Single().Cause);
        Assert.AreEqual(Cause.Placed(castle), game.History.EventsOf(game.History.SettlerId(19)).Single().Cause);
        Assert.AreEqual(22, game.History.Events.Count);
    }

    [TestMethod]
    public void PlacingAFlagAndABuildingRecordsOneEventEachCausePlacedWithTheThingCommanded()
    {
        var game = Castle();
        Do(game, CommandKind.PlaceFlag, 17, 3);
        Huts.PlaceAndTick(game, 16, 2);

        var flag = game.History.EventsOf(game.History.FlagId(1)).Single();
        var hut = game.History.EventsOf(game.History.BuildingId(1)).Single();

        Assert.AreEqual(EventType.PlaceFlag, flag.Type);
        Assert.AreEqual(1L, flag.Tick);
        Assert.AreEqual(Cause.Placed(game.History.FlagId(1)), flag.Cause);
        Assert.AreEqual(EventType.PlaceBuilding, hut.Type);
        Assert.AreEqual(2L, hut.Tick);
        Assert.AreEqual(Cause.Placed(game.History.BuildingId(1)), hut.Cause);
        Assert.AreEqual("flag#1.1", hut.Other, "the hut names its flag by the ID the flag has now");
        Assert.AreEqual(2, hut.State.Get("type"));
    }

    [TestMethod]
    public void BuildingARoadRecordsAnEventForEachEndFlagNamingTheOtherEnd()
    {
        var game = Castle();
        Do(game, CommandKind.PlaceFlag, 18, 6);
        Do(game, CommandKind.BuildRoad, 16, 6, [0, 0]);

        var from = game.History.EventsOf(game.History.FlagId(0)).Last();
        var to = game.History.EventsOf(game.History.FlagId(1)).Last();

        Assert.AreEqual(EventType.BuildRoad, from.Type);
        Assert.AreEqual(EventType.BuildRoad, to.Type);
        Assert.AreEqual(
            Cause.Placed(game.History.FlagId(0)),
            from.Cause,
            "the command's node is the road's first flag"
        );
        Assert.AreEqual(Cause.Placed(game.History.FlagId(0)), to.Cause);
        Assert.AreEqual("flag#1.1", from.Other);
        Assert.AreEqual("flag#0.1", to.Other);
        Assert.AreEqual(FlagEncoder.RoadByte(game.Flags[1].Side(3)), to.State.Get("road3"));
    }

    [TestMethod]
    public void TheStateOfAThingAtTheTickOfAnEventIsItsStateAtTheEndOfThatTick()
    {
        var game = World();
        var flag = game.History.FlagId(1);
        var placedAt = game.History.EventsOf(flag).First().Tick;

        var (state, refusal) = game.History.StateAt(flag, placedAt);

        Assert.IsNull(refusal);
        Assert.AreEqual(game.History.EventsOf(flag).First().State, state);
    }

    [TestMethod]
    public void SeveralEventsInOneTickResolveToTheLast()
    {
        var game = Castle();
        game.Enqueue(
            new Command
            {
                Tick = 1,
                Player = 0,
                Kind = CommandKind.PlaceFlag,
                X = 18,
                Y = 6,
            }
        );
        game.Enqueue(
            new Command
            {
                Tick = 1,
                Player = 0,
                Kind = CommandKind.PlaceFlag,
                X = 19,
                Y = 4,
            }
        );
        game.Enqueue(
            new Command
            {
                Tick = 1,
                Player = 0,
                Kind = CommandKind.BuildRoad,
                X = 18,
                Y = 6,
                Directions = [5, 5, 0],
            }
        );
        game.Tick();
        var flag = game.History.FlagId(1);

        var (state, _) = game.History.StateAt(flag, 1);

        Assert.HasCount(2, game.History.EventsOf(flag));
        Assert.AreEqual(game.History.EventsOf(flag)[1].State, state);
        Assert.AreEqual(FlagEncoder.RoadByte(game.Flags[1].Side(5)), state!.Get("road5"));
    }

    [TestMethod]
    public void ATickBeforeTheThingExistedAndAFutureTickAreRefusedWithAReason()
    {
        var game = Castle();
        Do(game, CommandKind.PlaceFlag, 18, 6);
        game.Tick();

        var (early, earlyReason) = game.History.StateAt(game.History.FlagId(1), 0);
        var (late, lateReason) = game.History.StateAt(game.History.FlagId(1), 9);

        Assert.IsNull(early);
        StringAssert.Contains(earlyReason, "flag#1.1");
        Assert.IsNull(late);
        StringAssert.Contains(lateReason, "9");
    }

    static string Text(Game game) =>
        string.Join(
            "\n",
            game.History.Events.All.Select(e => $"{e.Tick} {e.Type} {e.Thing} [{e.State}] {e.Cause} {e.Other}")
        );

    [TestMethod]
    public void EveryLayoutEntryRecordsEventsWithTheCauseLayoutAndNamesThingsByTheirIds()
    {
        var game = Laid();

        Assert.IsTrue(game.History.Events.All.All(e => e.Cause == Cause.Layout), Text(game));
        var inHut = game.History.Events.All.Single(e =>
            e.Type == EventType.LayoutInHut && e.Thing.Kind == ThingId.SettlerKind
        );
        var hut = game.History.Events.All.Single(e =>
            e.Type == EventType.LayoutInHut && e.Thing.Kind == ThingId.BuildingKind
        );
        Assert.AreEqual(hut.Thing.ToString(), inHut.Other);
        Assert.AreEqual(inHut.Thing.ToString(), hut.Other);
        Assert.AreEqual(game.Settlers[inHut.Thing.Slot].Activity.Code, inHut.State.Get("state"));
        var onRoad = game.History.Events.All.Where(e => e.Type == EventType.LayoutWaitingOnRoad).ToList();
        Assert.AreEqual(3, onRoad.Count, "the carrier and the flags at the two ends of its road");
        Assert.IsTrue(
            onRoad.Any(e =>
                e.Thing.Kind == ThingId.SettlerKind && e.Other!.StartsWith("flag#", StringComparison.Ordinal)
            )
        );
        var good = game.History.Events.All.Single(e => e.Type == EventType.LayoutGood);
        Assert.AreEqual(7, good.State.Get("good0"));
    }

    [TestMethod]
    public void TwoRunsOfTheSameLayoutAndOfTheSameCommandsGiveTheSameEvents()
    {
        Assert.AreEqual(Text(Laid()), Text(Laid()));
        Assert.AreEqual(Text(World()), Text(World()));
    }

    static List<string> Lines(Game game) =>
        [.. Thing.Of(game, new NameTable(MadeProgram.Made())).Select(thing => thing.Line())];

    [TestMethod]
    public void EveryFlagLineEndsWithItsId()
    {
        var lines = Lines(World()).Where(line => line.StartsWith("flag ", StringComparison.Ordinal)).ToList();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "flag 16,6 slot=0 id=flag#0.1",
                "flag 18,6 slot=1 id=flag#1.1",
                "flag 19,4 slot=2 id=flag#2.1",
                "flag 17,3 slot=3 id=flag#3.1",
            },
            lines
        );
    }

    [TestMethod]
    public void ABuildingLineShowsItsIdThenItsFlagAndItsWorkerAsIdsAfterEveryRawValue()
    {
        var game = World();
        var hut = game.Buildings[1];
        hut.Worker = 12;

        var line = Lines(game).Single(l => l.StartsWith("building 16,2 ", StringComparison.Ordinal));

        Assert.AreEqual(
            $"building 16,2 type=0x02 constructing=false status=0x{BuildingEncoder.Status(hut):x2} flag=3 worker={hut.Worker} "
                + $"id=building#1.1 flag-id=flag#3.1 worker-id={game.History.SettlerId(hut.Worker)} woodcutter-hut",
            line
        );
    }

    [TestMethod]
    public void ABurningBuildingShowsNoWorkerId()
    {
        var game = World();
        Do(game, CommandKind.RemoveBuilding, 16, 2);

        var line = Lines(game).Single(l => l.StartsWith("building 16,2 ", StringComparison.Ordinal));

        StringAssert.Contains(line, " id=building#1.1 flag-id=flag#3.1 woodcutter-hut");
        Assert.IsFalse(line.Contains("worker-id", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ASettlerLineShowsItsIdAndTheFlagItsStateMakesTheTargetAfterEveryRawValue()
    {
        var game = Laid();
        var carrier = game.Settlers.Select((s, slot) => (s, slot)).Single(e => e.s.Activity is WaitsAtFlag);
        var line = Lines(game)
            .Single(l => l.Contains($" id={game.History.SettlerId(carrier.slot)}", StringComparison.Ordinal));

        StringAssert.StartsWith(line, $"settler {carrier.s.X},{carrier.s.Y} job=0 ");
        StringAssert.Contains(
            line,
            $"stamp={SettlerEncoder.Fields(carrier.s, game.Tables.Catalog).Stamp} id=settler#{carrier.slot}.1 flag-id={game.History.FlagId(((WaitsAtFlag)carrier.s.Activity).Flag)} "
        );
        var waiting = Lines(game)
            .Count(l =>
                l.StartsWith("settler ", StringComparison.Ordinal) && l.Contains("flag-id=", StringComparison.Ordinal)
            );
        Assert.AreEqual(1, waiting, "a settler in the castle has no flag target");
    }
}
