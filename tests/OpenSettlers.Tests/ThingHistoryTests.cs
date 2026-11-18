using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;
using static OpenSettlers.Adapters.Testing.HistoryWorld;

namespace OpenSettlers.Tests;

// The causes a settler's events carry, which the game's history records through members internal to the application. They
// build their game through the adapters, so no test pair holds them (INV-0013): the rest of this class is in
// src/MapView.Tests. The map of the seed 1234,5678,9ABC with the castle at 15,5 (its flag at 16,6).
[TestClass]
public sealed class ThingHistoryTests
{
    static Game Castle()
    {
        var game = NewGame();
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();

        return game;
    }

    [TestMethod]
    public void ASettlerCarriesACommandCauseUntilItIdlesInStorageThenTakesTheRuleArea()
    {
        var game = Castle();
        var command = Cause.Placed(game.History.BuildingId(0));
        var settler = game.Settlers[5];
        game.History.CarryCause(5, command);

        settler.Activity = new Walks
        {
            Errand = new ToStorage(),
            Heading = new Heading { Value = 0 },
        };
        game.History.RecordSettler(5, EventType.PlaceCastle, Cause.Settlers);
        settler.Activity = new WaitsInCastle();
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
        settler.Activity = new WaitsAtFlag
        {
            Flag = 0,
            Direction = 0,
            Approach = new Heading { Value = 0 },
        };
        game.History.RecordSettler(5, EventType.PlaceCastle, Cause.Carriers);
        game.History.RecordSettler(5, EventType.PlaceCastle, Cause.Carriers);
        game.History.CarryCause(5, command);
        settler.Activity = new Walks
        {
            Errand = new ToStorage(),
            Heading = new Heading { Value = 0 },
        };
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
}
