using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The events of the walk home (ADR 0011): the claim of the node, the search, the arrival, and the entry into the castle, on the
// game of HomeWalkTests with a settler sent home as the teardown of a building sends it (state 0x1c).
[TestClass]
public sealed class HomeWalkHistoryTests
{
    private const int Sent = 12;

    private static Game SentHome(Cause? carried)
    {
        MapSize.TryFromValue(1, out var size, out _);
        var map = MapGenerator.Generate(TestProgram.Tables(), new MapSeed(0x1234, 0x5678, 0x9ABC), size!);
        var game = new Game(TestProgram.Tables(), map, 0);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 18, 20));
        game.Enqueue(new Command(0, 0, CommandKind.PlaceFlag, 16, 20));
        game.Enqueue(new Command(0, 0, CommandKind.BuildRoad, 19, 21, [3, 4, 3]));
        for (var pass = 0; pass < 5; pass++)
        {
            game.Tick();
        }

        game.Settlers[Sent].State = 0x1c;
        if (carried is not null)
        {
            game.CarryCause(Sent, carried);
        }

        for (var pass = 0; pass < 400 && game.Settlers[Sent].State != 1; pass++)
        {
            game.Tick();
        }

        Assert.AreEqual(1, game.Settlers[Sent].State, "the settler is home");

        return game;
    }

    private static ThingEvent[] Walk(Game game) =>
        [.. game.EventsOf(game.SettlerId(Sent)).Where(e => e.Type != EventType.PlaceCastle)];

    [TestMethod]
    public void ASettlerWalkingHomeRecordsEachChangeWithTheWalkHomeCause()
    {
        var walk = Walk(SentHome(null));

        CollectionAssert.AreEqual(
            new[] { EventType.ClaimedHome, EventType.SearchedHome, EventType.ArrivedHome, EventType.EnteredStorage },
            walk.Select(e => e.Type).Distinct().ToArray()
        );
        Assert.IsTrue(walk.All(e => e.Cause == Cause.WalkHome));
        Assert.AreEqual(0x19, walk.First(e => e.Type == EventType.ClaimedHome).State.Get("state"));
        Assert.AreEqual(1, walk[^1].State.Get("state"));
    }

    [TestMethod]
    public void ArrivingHomeNamesTheCastleAndEndsTheCarriedRazedCause()
    {
        var razed = Cause.Razed(new ThingId(ThingId.BuildingKind, 3, 1));
        var game = SentHome(razed);
        var walk = Walk(game);

        var arrived = walk.Single(e => e.Type == EventType.ArrivedHome);
        Assert.AreEqual(razed, arrived.Cause);
        Assert.AreEqual(game.BuildingId(0).ToString(), arrived.Other);
        Assert.IsTrue(
            walk.TakeWhile(e => e.Type != EventType.ArrivedHome).All(e => e.Cause == razed),
            "the changes before the arrival carry the razed cause"
        );
        Assert.AreEqual(Cause.WalkHome, walk.Single(e => e.Type == EventType.EnteredStorage).Cause);
    }
}
