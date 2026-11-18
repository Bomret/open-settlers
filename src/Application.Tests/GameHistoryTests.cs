using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The game history as its own part (ADR 0011, ADR 0013, INV-0007).
[TestClass]
public sealed class GameHistoryTests
{
    // A stand-in for the exporter: every thing's state is its slot.
    sealed class SlotSnapshots : IThingSnapshots
    {
        public ThingState Building(int slot) => new() { Fields = [("slot", slot)] };

        public ThingState Flag(int slot) => new() { Fields = [("slot", slot)] };

        public ThingState Settler(int slot) => new() { Fields = [("slot", slot), ("state", State)] };

        public string GoodText(Good good) => good.Name;

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
        const BindingFlags All =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;

        var left = typeof(Game).GetMembers(All).Select(m => m.Name).Where(historyNames.Contains).ToArray();

        Assert.IsEmpty(left);
        Assert.IsNotNull(typeof(Game).GetProperty(nameof(Game.History)));
    }
}
