using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A road drawn with the Road button that ends on a node of another road is refused (docs/rules/roads.md):
// road mode does not hold view flag bit 3, so the click step refuses a road node as the error sound. The game is
// the one of RoadSplitGameTests: the castle at 15,5 with its flag at 16,6, a flag at 20,6, and a road east between them whose nodes
// 17,6 to 19,6 hold no flag. A flag at 18,8 starts the roads drawn toward them.
[TestClass]
public sealed class RoadOnRoadTests
{
    private static Game Started()
    {
        var map = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        var game = new Game(RemovalLogicTests.Made(), map);
        Apply(game, new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        Apply(game, new Command(game.TickCount, 0, CommandKind.PlaceFlag, 20, 6));
        Apply(game, new Command(game.TickCount, 0, CommandKind.PlaceFlag, 18, 8));
        Apply(game, new Command(game.TickCount, 0, CommandKind.BuildRoad, 16, 6, [0, 0, 0, 0]));
        Assert.AreEqual(1, game.Roads.Count, "the east road is built");

        return game;
    }

    private static void Apply(Game game, Command command)
    {
        game.Enqueue(command);
        game.Tick();
    }

    // From 18,8 north to 18,7, then north onto 18,6, a node of the east road.
    private static readonly int[] OntoTheRoad = [5, 5];

    [TestMethod]
    public void ARoadWhoseLastStepLandsOnTheNodeOfAnotherRoadIsRefused()
    {
        var game = Started();

        Assert.AreEqual(RoadStatus.Open, game.CheckRoad(18, 8, [5]));
        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(18, 8, OntoTheRoad));
    }

    [TestMethod]
    public void ARefusedRoadOnARoadLeavesTheOtherRoadAndTheMapAsTheyWere()
    {
        var game = Started();
        var flags = game.Map.Digest(MapLayer.Flags);
        var road = game.Roads.Single();

        Apply(game, new Command(game.TickCount, 0, CommandKind.BuildRoad, 18, 8, OntoTheRoad));

        Assert.AreEqual(1, game.Roads.Count);
        Assert.AreEqual(road, game.Roads.Single());
        Assert.AreEqual(flags, game.Map.Digest(MapLayer.Flags));
        Assert.IsFalse(game.Events.All.Any(e => e.Type == EventType.SplitRoads), "no split");
    }

    [TestMethod]
    public void TheClickThatLandsOnTheRoadIsIgnoredAndTheRoadBeingDrawnStays()
    {
        var game = Started();
        var drawing = new RoadDrawing(game);
        drawing.Click(18, 8);
        drawing.Click(18, 7);

        Assert.IsNull(drawing.Click(18, 6));

        CollectionAssert.AreEqual(new[] { (18, 8), (18, 7) }, drawing.Nodes.ToArray());
        Assert.AreEqual(1, game.Roads.Count);
    }

    [TestMethod]
    public void ATakenBackStepNextToTheOtherRoadLeavesTheOtherRoadFlagsAndEventsAsTheyWere()
    {
        var game = Started();
        var flags = game.Map.Digest(MapLayer.Flags);
        var road = game.Roads.Single();
        var events = game.Events.All.Count();
        var drawing = new RoadDrawing(game);
        drawing.Click(18, 8);
        drawing.Click(18, 7);
        drawing.Click(18, 6);

        Assert.IsNull(drawing.Click(18, 8));

        CollectionAssert.AreEqual(new[] { (18, 8) }, drawing.Nodes.ToArray());
        AssertUnchanged(game, road, flags, events);
    }

    [TestMethod]
    public void ACancelledRoadNextToTheOtherRoadLeavesTheOtherRoadFlagsAndEventsAsTheyWere()
    {
        var game = Started();
        var flags = game.Map.Digest(MapLayer.Flags);
        var road = game.Roads.Single();
        var events = game.Events.All.Count();
        var drawing = new RoadDrawing(game);
        drawing.Click(18, 8);
        drawing.Click(18, 7);
        drawing.Click(18, 6);

        drawing.Cancel();

        Assert.IsFalse(drawing.Active);
        AssertUnchanged(game, road, flags, events);
    }

    private static void AssertUnchanged(Game game, Road road, ulong flags, int events)
    {
        Assert.AreEqual(1, game.Roads.Count);
        Assert.AreEqual(road, game.Roads.Single());
        Assert.AreEqual(flags, game.Map.Digest(MapLayer.Flags));
        Assert.AreEqual(events, game.Events.All.Count());
    }

    [TestMethod]
    public void ARoadOntoItsOwnEarlierNodeOrAcrossAnotherRoadStaysRefused()
    {
        var game = Started();

        // From 18,8 north, east, south: the next step north-west lands on the road's own earlier node 18,7.
        Assert.AreEqual(RoadStatus.Open, game.CheckRoad(18, 8, [5, 0, 2]));
        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(18, 8, [5, 0, 2, 4]), "its own earlier node");
        Assert.AreEqual(RoadStatus.Refused, game.CheckRoad(18, 8, [5, 5, 5]), "across the east road");
    }
}
