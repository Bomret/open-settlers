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
            game.History.CarryCause(Sent, carried);
        }

        for (var pass = 0; pass < 400 && game.Settlers[Sent].State != 1; pass++)
        {
            game.Tick();
        }

        Assert.AreEqual(1, game.Settlers[Sent].State, "the settler is home");

        return game;
    }

    private static ThingEvent[] Walk(Game game) =>
        [.. game.History.EventsOf(game.History.SettlerId(Sent)).Where(e => e.Type != EventType.PlaceCastle)];

    [TestMethod]
    public void ASettlerWalkingHomeRecordsEachChangeWithTheWalkHomeCause()
    {
        var walk = Walk(SentHome(null));
        var types = walk.Select(e => e.Type).Distinct().ToList();

        CollectionAssert.IsSubsetOf(
            new[] { EventType.ClaimedHome, EventType.SearchedHome, EventType.ArrivedHome, EventType.EnteredStorage },
            types
        );
        Assert.IsTrue(types.Contains(EventType.Walked), "the walk along the roads is recorded");
        Assert.IsTrue(walk.All(e => e.Cause == Cause.WalkHome), "every change carries walk-home");
        Assert.AreEqual(0x19, walk.First(e => e.Type == EventType.ClaimedHome).State.Get("state"));
        Assert.AreEqual(1, walk[^1].State.Get("state"));
    }

    [TestMethod]
    public void ARazedSettlerKeepsTheRazedCauseUntilItIsInTheCastle()
    {
        var razed = Cause.Razed(new ThingId(ThingId.BuildingKind, 3, 1));
        var game = SentHome(razed);
        var walk = Walk(game);

        var arrived = walk.Single(e => e.Type == EventType.ArrivedHome);
        Assert.AreEqual(razed, arrived.Cause);
        Assert.AreEqual(game.History.BuildingId(0).ToString(), arrived.Other);
        Assert.IsTrue(walk.Any(e => e.Type == EventType.Walked), "the road walk is recorded");
        Assert.IsTrue(
            walk.All(e => e.Cause == razed),
            "every change, the road steps and the entry included, carries razed"
        );
    }

    // A free walk home that ends on a node that is no flag with a road: the arrival names no storage and the settler searches again.
    [TestMethod]
    public void ArrivingOnANodeThatIsNoHomeFlagSearchesAgainAndKeepsTheCarry()
    {
        var razed = Cause.Razed(new ThingId(ThingId.BuildingKind, 3, 1));
        var game = SentHome(razed);
        var settler = game.Settlers[Sent];
        settler.X = 30;
        settler.Y = 30;
        settler.State = 0x10;
        settler.Mode = 0;
        settler.Target = 0x8000;
        settler.Heading = -1;
        settler.Next = 8;
        settler.Timer = 0;
        game.History.CarryCause(Sent, razed);
        for (var pass = 0; pass < 200 && settler.State == 0x10; pass++)
        {
            game.Tick();
        }

        var arrived = game.History.EventsOf(game.History.SettlerId(Sent)).Last(e => e.Type == EventType.ArrivedHome);
        Assert.IsNull(arrived.Other);
        Assert.AreEqual(0x19, arrived.State.Get("state"));
        Assert.AreEqual(razed, arrived.Cause);
        game.Tick();
        Assert.AreEqual(razed, game.History.EventsOf(game.History.SettlerId(Sent)).Last().Cause, "the carry goes on");
    }

    [TestMethod]
    public void AKnightSearchingHomeTakesState0x35()
    {
        var game = SentHome(null);
        var knight = game.Settlers.First(s => s.Job == 22);
        var slot = game.Settlers.ToList().IndexOf(knight);
        knight.State = 0x1c;
        for (var pass = 0; pass < 80 && knight.State != 0x35; pass++)
        {
            game.Tick();
        }

        Assert.AreEqual(0x35, knight.State);
        var searched = game.History.EventsOf(game.History.SettlerId(slot)).Last(e => e.Type == EventType.SearchedHome);
        Assert.AreEqual(0x35, searched.State.Get("state"));
        Assert.AreEqual(Cause.WalkHome, searched.Cause);
    }
}
