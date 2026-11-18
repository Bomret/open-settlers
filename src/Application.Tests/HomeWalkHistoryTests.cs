using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The razed cause a settler carries along the walk home (ADR 0011), which the game's history carries through members internal to the
// application: the bench's game on the made-up tables (MadeProgram.MadeTables, which hold the free walk's directions) and its flat map, laid as the install's game of the class of the same
// name in src/Adapters.Tests lays it. The castle stands at 18,20, a flag at 16,20, and a road from the castle's flag at 19,21; the
// castle's settler in slot 12 is sent home as the teardown of a building sends it (state 0x1c), carrying the razed cause.
[TestClass]
public sealed class HomeWalkHistoryTests
{
    const int Sent = 12;

    static readonly Cause Razed = Cause.Razed(
        new ThingId
        {
            Kind = ThingId.BuildingKind,
            Slot = 3,
            Fill = 1,
        }
    );

    static Game SentHome()
    {
        var game = GameBench.Create(MadeProgram.MadeTables(), GameBench.Flat());
        // The three commands apply at the tick 0, as the install's game applies them, and the game runs five passes.
        Assert.IsTrue(GameBench.Apply(game, CommandKind.PlaceCastle, 18, 20));
        Assert.IsTrue(GameBench.Apply(game, CommandKind.PlaceFlag, 16, 20));
        Assert.IsTrue(GameBench.Apply(game, CommandKind.BuildRoad, 19, 21, [3, 4, 3]));
        GameBench.Run(game, 5);
        Assert.AreEqual(1, game.Network.Roads.Count, "the road is built");

        game.Settlers[Sent].Activity = new ClaimsHomeNode();
        game.History.CarryCause(Sent, Razed);
        for (var pass = 0; pass < 400 && game.Settlers[Sent].Activity is not WaitsInCastle; pass++)
        {
            game.Tick();
        }

        Assert.IsInstanceOfType<WaitsInCastle>(game.Settlers[Sent].Activity, "the settler is home");

        return game;
    }

    static ThingEvent[] Walk(Game game) =>
        [.. game.History.EventsOf(game.History.SettlerId(Sent)).Where(e => e.Type != EventType.PlaceCastle)];

    // The carry's half: the arrival and every change carry razed. The arrival's storage on the install's game is in src/Adapters.Tests.
    [TestMethod]
    public void ARazedSettlerKeepsTheRazedCauseUntilItIsInTheCastle()
    {
        var game = SentHome();
        var walk = Walk(game);

        var arrived = walk.Single(e => e.Type == EventType.ArrivedHome);
        Assert.AreEqual(Razed, arrived.Cause);
        Assert.AreEqual(game.History.BuildingId(0).ToString(), arrived.Other);
        Assert.IsTrue(walk.Any(e => e.Type == EventType.Walked), "the road walk is recorded");
        Assert.IsTrue(
            walk.All(e => e.Cause == Razed),
            "every change, the road steps and the entry included, carries razed"
        );
    }

    // A free walk home that ends on a node that is no flag with a road: the arrival names no storage and the settler searches again,
    // still carrying razed.
    [TestMethod]
    public void ArrivingOnANodeThatIsNoHomeFlagSearchesAgainAndKeepsTheCarry()
    {
        var game = SentHome();
        var settler = game.Settlers[Sent];
        settler.X = 30;
        settler.Y = 30;
        settler.Activity = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = new Vector(0, 0),
                Purpose = new Home(),
                Memory = 8,
            },
        };
        settler.Timer = 0;
        game.History.CarryCause(Sent, Razed);
        for (var pass = 0; pass < 200 && settler.Activity is WalksFree; pass++)
        {
            game.Tick();
        }

        var arrived = game.History.EventsOf(game.History.SettlerId(Sent)).Last(e => e.Type == EventType.ArrivedHome);
        Assert.IsNull(arrived.Other);
        Assert.AreEqual(0x19, arrived.State.Get("state"));
        Assert.AreEqual(Razed, arrived.Cause);
        game.Tick();
        Assert.AreEqual(Razed, game.History.EventsOf(game.History.SettlerId(Sent)).Last().Cause, "the carry goes on");
    }
}
