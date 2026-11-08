using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The game history as its own part (ADR 0011, ADR 0013, INV-0007) and the exporter that writes the original's raw values
// into each event's state (INV-0006).
[TestClass]
public sealed class GameHistoryTests
{
    // A stand-in for the exporter: every thing's state is its slot.
    private sealed class SlotSnapshots : IThingSnapshots
    {
        public ThingState Building(int slot) => new([("slot", slot)]);

        public ThingState Flag(int slot) => new([("slot", slot)]);

        public ThingState Settler(int slot) => new([("slot", slot), ("state", State)]);

        public int State { get; set; }
    }

    [TestMethod]
    public void TheHistoryNeedsNoGameToRecordAndAnswerAnEvent()
    {
        var tick = 4L;
        var history = new GameHistory(new SlotSnapshots(), () => tick);
        history.FillBuilding(2);

        history.RecordBuilding(2, EventType.PlaceCastle, Cause.Settlers);

        var thing = history.BuildingId(2);
        Assert.AreEqual("building#2.1", thing.ToString());
        Assert.AreEqual(4, history.EventsOf(thing).Single().Tick);
    }

    [TestMethod]
    public void AStateBeforeAThingsFirstEventIsRefusedWithTheTickOfItsFirstEvent()
    {
        var tick = 3L;
        var history = new GameHistory(new SlotSnapshots(), () => tick);
        history.RecordBuilding(0, EventType.PlaceCastle, Cause.Settlers);
        tick = 5;

        var (state, refusal) = history.StateAt(history.BuildingId(0), 1);

        Assert.IsNull(state);
        Assert.AreEqual("building#0.0 first appears at tick 3, after tick 1", refusal);
    }

    [TestMethod]
    public void ACarriedCauseEndsWithTheEventThatLeavesTheSettlerIdle()
    {
        var snapshots = new SlotSnapshots { State = 2 };
        var history = new GameHistory(snapshots, () => 0);
        var command = Cause.Placed(history.BuildingId(0));
        history.CarryCause(0, command);
        history.RecordSettler(0, EventType.PlaceCastle, Cause.Settlers);
        snapshots.State = 1;
        history.RecordSettler(0, EventType.PlaceCastle, Cause.Settlers);
        history.RecordSettler(0, EventType.PlaceCastle, Cause.Settlers);

        var causes = history.EventsOf(history.SettlerId(0)).Select(e => e.Cause).ToArray();

        CollectionAssert.AreEqual(new[] { command, command, Cause.Settlers }, causes);
    }

    [TestMethod]
    public void TheExporterWritesTheSettlersRawValuesAsNamedFieldsInOrder()
    {
        var settler = new Settler(job: 5, player: 0, x: 7, y: 9, stamp: 11, state: 0x42, home: 3)
        {
            Mode = 0xfe,
            Target = 300,
            Next = 15,
            Frame = 6,
            Timer = 65535,
        };
        var exporter = new SnapshotExporter([settler], [], [], [], [], []);

        var state = exporter.Settler(0);

        Assert.AreEqual(
            "job=5 state=66 player=0 mode=254 target=300 next=15 heading=3 frame=6 timer=65535 stamp=11 x=7 y=9",
            state.ToString()
        );
    }

    [TestMethod]
    public void TheExporterWritesAFlagsRoadBytesAndGoodsAfterItsNodeAndStatus()
    {
        var exporter = new SnapshotExporter(
            [],
            [],
            [new Flag(4, 5)],
            [0x81],
            [
                [1, 2, 3, 4, 5, 6],
            ],
            [
                [9, 0, 0, 0, 0, 0, 0, 0x21],
            ]
        );

        var state = exporter.Flag(0);

        Assert.AreEqual(
            "x=4 y=5 status=129 road0=1 road1=2 road2=3 road3=4 road4=5 road5=6 good0=9 good1=0 good2=0 good3=0 good4=0 good5=0 good6=0 good7=33",
            state.ToString()
        );
    }

    [TestMethod]
    public void TheGameHoldsNoHistoryMemberBesidesTheHistoryProperty()
    {
        var historyNames = new[]
        {
            "Events",
            "EventsOf",
            "SettlerId",
            "BuildingId",
            "FlagId",
            "StateAt",
            "CarryCause",
            "EndCarry",
            "RecordBuilding",
            "RecordFlag",
            "RecordSettler",
            "Record",
        };
        const BindingFlags all =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;

        var left = typeof(Game).GetMembers(all).Select(m => m.Name).Where(historyNames.Contains).ToArray();

        Assert.IsEmpty(left);
        Assert.IsNotNull(typeof(Game).GetProperty(nameof(Game.History)));
    }

    [TestMethod]
    public void TheGameRecordsThroughItsHistory()
    {
        var game = ThingHistoryTests.NewGame();
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();

        Assert.IsNotEmpty(game.History.EventsOf(game.History.BuildingId(0)));
    }
}
