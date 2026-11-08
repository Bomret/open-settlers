using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Thing IDs, the event store, causes, and the list file's ids (ADR 0011), on the map of the seed 1234,5678,9ABC with the
// castle at 15,5 (its flag at 16,6).
[TestClass]
public sealed class ThingHistoryTests
{
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

    internal static Game NewGame()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        return new Game(Made(), map);
    }

    private static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(new Command(game.TickCount, 0, kind, x, y, directions));
        game.Tick();
    }

    // The world of GameThingTests: flags at 18,6 (slot 1), 19,4 (2), 17,3 (3), roads between them, a hut at 16,2.
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
        for (var i = 0; i < 60; i++)
        {
            game.Tick();
        }

        return game;
    }

    [TestMethod]
    public void TheDigestOfAFixedGameIsTheOneTakenBeforeThingHistory()
    {
        Assert.AreEqual(15684556580687199582UL, World().Digest());
    }

    private static Game Castle()
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
        Do(game, CommandKind.PlaceBuilding, 16, 2);
        Assert.AreEqual("building#0.1", game.History.BuildingId(0).ToString());
        Assert.AreEqual("building#1.1", game.History.BuildingId(1).ToString());

        Do(game, CommandKind.RemoveBuilding, 16, 2);
        for (var i = 0; i < 3000 && game.Buildings.Count > 1; i++)
        {
            game.Tick();
        }

        Assert.HasCount(1, game.Buildings);
        Do(game, CommandKind.PlaceFlag, 17, 3);
        Do(game, CommandKind.PlaceBuilding, 16, 2);
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
        Do(game, CommandKind.PlaceBuilding, 16, 2);

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
        Assert.AreEqual(game.RoadByte(1, 3), to.State.Get("road3"));
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
        game.Enqueue(new Command(1, 0, CommandKind.PlaceFlag, 18, 6));
        game.Enqueue(new Command(1, 0, CommandKind.PlaceFlag, 19, 4));
        game.Enqueue(new Command(1, 0, CommandKind.BuildRoad, 18, 6, [5, 5, 0]));
        game.Tick();
        var flag = game.History.FlagId(1);

        var (state, _) = game.History.StateAt(flag, 1);

        Assert.HasCount(2, game.History.EventsOf(flag));
        Assert.AreEqual(game.History.EventsOf(flag)[1].State, state);
        Assert.AreEqual(game.RoadByte(1, 5), state!.Get("road5"));
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

    [TestMethod]
    public void ASettlerCarriesACommandCauseUntilItIdlesInStorageThenTakesTheRuleArea()
    {
        var game = Castle();
        var command = Cause.Placed(game.History.BuildingId(0));
        var settler = game.Settlers[5];
        game.History.CarryCause(5, command);

        settler.State = 2;
        game.History.RecordSettler(5, EventType.PlaceCastle, Cause.Settlers);
        settler.State = 1;
        game.History.RecordSettler(5, EventType.PlaceCastle, Cause.Settlers);
        game.History.RecordSettler(5, EventType.PlaceCastle, Cause.Settlers);

        var causes = game.History.EventsOf(game.History.SettlerId(5)).Skip(1).Select(e => e.Cause).ToArray();
        CollectionAssert.AreEqual(new[] { command, command, Cause.Settlers }, causes);
    }

    [TestMethod]
    public void ASettlerWaitingAtAFlagEndsTheCarryAndATakenJobRequestEndsItBeforeTheEvent()
    {
        var game = Castle();
        var command = Cause.Razed(game.History.BuildingId(0));
        var settler = game.Settlers[5];

        game.History.CarryCause(5, command);
        settler.State = 0x42;
        game.History.RecordSettler(5, EventType.PlaceCastle, Cause.Carriers);
        game.History.RecordSettler(5, EventType.PlaceCastle, Cause.Carriers);
        game.History.CarryCause(5, command);
        settler.State = 2;
        game.History.EndCarry(5);
        game.History.RecordSettler(5, EventType.PlaceCastle, Cause.Settlers);

        var causes = game.History.EventsOf(game.History.SettlerId(5)).Skip(1).Select(e => e.Cause).ToArray();
        CollectionAssert.AreEqual(new[] { command, Cause.Carriers, Cause.Settlers }, causes);
    }

    [TestMethod]
    public void OnlyACommandCauseCanBeCarried()
    {
        var game = Castle();

        Assert.ThrowsExactly<ArgumentException>(() => game.History.CarryCause(5, Cause.Woodcutting));
    }

    private const string Built = """
        "castle": {"x": 10, "y": 10},
        "flags": [{"x": 14, "y": 11}],
        "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}],
        "buildings": [{"x": 13, "y": 10}],
        "settlers": [
          {"job": "woodcutter", "state": "in-hut", "x": 13, "y": 10},
          {"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11, "direction": 0}
        ],
        "goods": [{"x": 14, "y": 11, "place": 0, "good": 7}]
        """;

    private static Game Laid(string extra = Built)
    {
        var read = Layout.Read(LayoutTests.Text(extra: "\n" + extra), "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));
        var game = LayoutSettlersTests.NewGame(read.Layout!);
        Assert.IsNull(read.Layout!.Apply(game));

        return game;
    }

    private static string Text(Game game) =>
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
        Assert.AreEqual(game.Settlers[inHut.Thing.Slot].State, inHut.State.Get("state"));
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

    private static List<string> Lines(Game game) =>
        [.. Thing.Of(game, new NameTable(TestProgram.Made())).Select(thing => thing.Line())];

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
            $"building 16,2 type=0x02 constructing=false status=0x{hut.Status:x2} flag=3 worker={hut.Worker} "
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
        var carrier = game.Settlers.Select((s, slot) => (s, slot)).Single(e => e.s.State == 0x42);
        var line = Lines(game)
            .Single(l => l.Contains($" id={game.History.SettlerId(carrier.slot)}", StringComparison.Ordinal));

        StringAssert.StartsWith(line, $"settler {carrier.s.X},{carrier.s.Y} job=0 ");
        StringAssert.Contains(
            line,
            $"stamp={carrier.s.Stamp} id=settler#{carrier.slot}.1 flag-id={game.History.FlagId(carrier.s.Target)} "
        );
        var waiting = Lines(game)
            .Count(l =>
                l.StartsWith("settler ", StringComparison.Ordinal) && l.Contains("flag-id=", StringComparison.Ordinal)
            );
        Assert.AreEqual(1, waiting, "a settler in the castle has no flag target");
    }
}
