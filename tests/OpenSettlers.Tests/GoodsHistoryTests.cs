using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The events of the goods rules' changes to a carrier, a flag, and the castle (ADR 0011), on the layout of
// layouts/carrier.jsonc: a carrier on the road from the castle's flag to the flag at 14,11, and two logs on that flag.
[TestClass]
public sealed class GoodsHistoryTests
{
    private const string Carry = """
        "castle": {"x": 10, "y": 10},
        "flags": [{"x": 14, "y": 11}],
        "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}],
        "settlers": [{"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11, "direction": 0}],
        "goods": [
          {"x": 14, "y": 11, "place": 0, "good": 7},
          {"x": 14, "y": 11, "place": 1, "good": 7}
        ]
        """;

    // Runs the carry until the castle holds a delivered log, or fails.
    private static Game Carried()
    {
        var read = Layout.Read(LayoutTests.Text(extra: "\n" + Carry), "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));
        var game = LayoutSettlersTests.NewGame(read.Layout!);
        Assert.IsNull(read.Layout!.Apply(game));
        for (var i = 0; i < 4000 && game.Events.All.All(e => e.Type != EventType.Handover); i++)
        {
            game.Tick();
        }

        return game;
    }

    private static ThingId Carrier(Game game) =>
        game.Events.All.First(e => e.Thing.Kind == ThingId.SettlerKind && e.Type == EventType.LayoutWaitingOnRoad).Thing;

    [TestMethod]
    public void ACarrierThatPicksUpAndDeliversAGoodRecordsEachChangeNamingTheFlagAndTheBuilding()
    {
        var game = Carried();
        var events = game.EventsOf(Carrier(game));
        var types = events.Select(e => e.Type).ToHashSet();

        Assert.IsTrue(types.IsSupersetOf([EventType.Fetch, EventType.Claim, EventType.Exchange, EventType.Deliver, EventType.Handover]));
        var picked = events.First(e => e.Type == EventType.Exchange);
        StringAssert.StartsWith(picked.Other, game.FlagId(1).ToString());
        Assert.AreEqual(7, picked.State.Get("mode"), "the log is in the carrier's hands");
        var castle = game.EventsOf(game.BuildingId(0)).Single(e => e.Type == EventType.Handover);
        StringAssert.Contains(castle.Other!, Carrier(game).ToString());
        StringAssert.Contains(castle.Other!, "good 7");
    }

    [TestMethod]
    public void TheRulesOfTheGoodsCarryTheCauseGoods()
    {
        var game = Carried();
        var ours = new[] { EventType.Fetch, EventType.Claim, EventType.Exchange, EventType.Deliver, EventType.Handover };

        Assert.IsTrue(game.Events.All.Where(e => ours.Contains(e.Type)).All(e => e.Cause == Cause.Goods));
    }

    [TestMethod]
    public void TheStateOfTheCarrierAtEveryTickOfTheCarryIsTheEnginesStateThen()
    {
        var read = Layout.Read(LayoutTests.Text(extra: "\n" + Carry), "test.jsonc");
        var game = LayoutSettlersTests.NewGame(read.Layout!);
        Assert.IsNull(read.Layout!.Apply(game));
        var id = Carrier(game);
        var slot = id.Slot;
        var seen = new List<(long Tick, string Engine, string? Recorded)>();
        for (var i = 0; i < 4000 && game.Events.All.All(e => e.Type != EventType.Handover); i++)
        {
            game.Tick();
            var tick = game.TickCount - 1;
            var s = game.Settlers[slot];
            if (game.EventsOf(id).Any(e => e.Tick == tick && GoodsTypes.Contains(e.Type)))
            {
                var last = game.EventsOf(id).Last(e => e.Tick == tick);
                // The walking that goes on in the same tick (frame, timer, heading) is the carry walk's events.
                var engine = $"{s.State} {s.Mode} {s.Target}";
                var state = last.State;
                seen.Add((tick, engine, $"{state.Get("state")} {state.Get("mode")} {state.Get("target")}"));
            }
        }

        Assert.IsTrue(seen.Count >= 3, "pickup, exchange, and delivery each have a tick");
        foreach (var (tick, engine, recorded) in seen)
        {
            Assert.AreEqual(engine, recorded, $"tick {tick}");
        }
    }

    private static readonly string[] GoodsTypes =
        [EventType.Fetch, EventType.Claim, EventType.Exchange, EventType.Deliver, EventType.Handover];
}
