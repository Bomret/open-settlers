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
        for (var i = 0; i < 4000 && game.History.Events.All.All(e => e.Type != EventType.Handover); i++)
        {
            game.Tick();
        }

        return game;
    }

    private static ThingId Carrier(Game game) =>
        game
            .History.Events.All.First(e =>
                e.Thing.Kind == ThingId.SettlerKind && e.Type == EventType.LayoutWaitingOnRoad
            )
            .Thing;

    [TestMethod]
    public void ACarrierThatPicksUpAndDeliversAGoodRecordsEachChangeNamingTheFlagAndTheBuilding()
    {
        var game = Carried();
        var events = game.History.EventsOf(Carrier(game));
        var types = events.Select(e => e.Type).ToHashSet();

        Assert.IsTrue(
            types.IsSupersetOf([
                EventType.Fetch,
                EventType.Claim,
                EventType.Exchange,
                EventType.Deliver,
                EventType.Handover,
            ])
        );
        var picked = events.First(e => e.Type == EventType.Exchange);
        StringAssert.StartsWith(picked.Other, game.History.FlagId(1).ToString());
        Assert.AreEqual(7, picked.State.Get("mode"), "the log is in the carrier's hands");
        var castle = game.History.EventsOf(game.History.BuildingId(0)).Single(e => e.Type == EventType.Handover);
        StringAssert.Contains(castle.Other!, Carrier(game).ToString());
        StringAssert.Contains(castle.Other!, "good 7");
    }

    [TestMethod]
    public void TheRulesOfTheGoodsCarryTheCauseGoods()
    {
        var game = Carried();
        var ours = new[]
        {
            EventType.Fetch,
            EventType.Claim,
            EventType.Exchange,
            EventType.Deliver,
            EventType.Handover,
        };

        Assert.IsTrue(game.History.Events.All.Where(e => ours.Contains(e.Type)).All(e => e.Cause == Cause.Goods));
    }

    private static readonly string[] Fields =
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

    [TestMethod]
    public void TheStateOfTheCarrierAtEveryTickOfTheCarryIsTheEnginesStateThen()
    {
        var read = Layout.Read(LayoutTests.Text(extra: "\n" + Carry), "test.jsonc");
        var game = LayoutSettlersTests.NewGame(read.Layout!);
        Assert.IsNull(read.Layout!.Apply(game));
        var id = Carrier(game);
        var ticks = 0;
        for (var i = 0; i < 4000 && game.History.Events.All.All(e => e.Type != EventType.Handover); i++)
        {
            game.Tick();
            var s = game.Settlers[id.Slot];
            int[] engine =
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
            var (state, refusal) = game.History.StateAt(id, game.TickCount - 1);
            Assert.IsNotNull(state, refusal);
            CollectionAssert.AreEqual(
                engine,
                Fields.Select(f => state.Get(f)!.Value).ToArray(),
                $"tick {game.TickCount - 1}"
            );
            ticks++;
        }

        Assert.IsTrue(
            game.History.EventsOf(id).Any(e => e.Type == EventType.Handover),
            "the carry ended in a delivery"
        );
        Assert.IsGreaterThan(50, ticks, "the carry spans many ticks");
    }

    [TestMethod]
    public void ADeliveryRecordsTheHalfStepAndTheSecondTimeAtTheFlagAsTwoHandoverEventsOfTheCarrier()
    {
        var game = Carried();
        var id = Carrier(game);
        for (var i = 0; i < 4000 && game.History.EventsOf(id).Count(e => e.Type == EventType.Handover) < 2; i++)
        {
            game.Tick();
        }

        var handovers = game.History.EventsOf(id).Where(e => e.Type == EventType.Handover).ToList();

        Assert.IsGreaterThanOrEqualTo(2, handovers.Count);
        Assert.IsNotNull(handovers[0].Other, "the first names the building and the good");
        Assert.IsNull(handovers[1].Other, "the second, with Next set, names none");
    }
}
