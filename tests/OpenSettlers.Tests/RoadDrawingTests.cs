using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The stand-in interface draws a road a step at a time and sends one BuildRoad command when it reaches a flag. The
// game is the one of RoadsTests: the castle's flag at 16,6 and a second flag at 18,6.
[TestClass]
public sealed class RoadDrawingTests
{
    [TestMethod]
    public void AClickOnAFlagStartsARoadAndTheClicksOnTheNextNodesTakeItStepByStep()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);

        Assert.IsNull(drawing.Click(16, 6));
        Assert.IsTrue(drawing.Active);
        Assert.IsNull(drawing.Click(17, 6));

        CollectionAssert.AreEqual(new[] { (16, 6), (17, 6) }, drawing.Nodes.ToArray());
        Assert.AreEqual(0, game.Roads.Count);
    }

    [TestMethod]
    public void TheStepThatReachesAFlagEndsTheDrawingAndHandsBackTheCommandThatBuildsTheRoad()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);
        drawing.Click(16, 6);
        drawing.Click(17, 6);

        var command = drawing.Click(18, 6);

        Assert.IsNotNull(command);
        Assert.AreEqual(CommandKind.BuildRoad, command.Value.Kind);
        CollectionAssert.AreEqual(new[] { 0, 0 }, command.Value.Directions!.ToArray());
        Assert.IsFalse(drawing.Active);
        game.Enqueue(command.Value);
        game.Tick();
        Assert.AreEqual(1, game.Roads.Count);
    }

    [TestMethod]
    public void AClickOnTheNodeBeforeTheEndTakesTheLastStepBack()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);
        drawing.Click(16, 6);
        drawing.Click(17, 6);

        Assert.IsNull(drawing.Click(16, 6));

        CollectionAssert.AreEqual(new[] { (16, 6) }, drawing.Nodes.ToArray());
    }

    [TestMethod]
    public void ARefusedStepAndAClickAwayFromTheEndChangeNothing()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);
        drawing.Click(16, 6);

        // The castle stands north-west of its flag: a road may not run into it. 20,6 is no neighbour of the start.
        Assert.IsNull(drawing.Click(15, 5));
        Assert.IsNull(drawing.Click(20, 6));

        CollectionAssert.AreEqual(new[] { (16, 6) }, drawing.Nodes.ToArray());
    }

    [TestMethod]
    public void ACancelledRoadLeavesNoStateBehindAndANodeWithoutAFlagStartsNone()
    {
        var game = RoadsTests.TwoFlags();
        var same = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);

        drawing.Click(17, 6);
        Assert.IsFalse(drawing.Active);
        drawing.Click(16, 6);
        drawing.Click(17, 6);
        drawing.Cancel();
        game.Tick();
        same.Tick();

        Assert.IsFalse(drawing.Active);
        Assert.IsEmpty(drawing.Nodes);
        Assert.AreEqual(same.Digest(), game.Digest());
    }

    [TestMethod]
    public void ARoadOverTheWrappingEdgeContinuesInUnwrappedCoordinates()
    {
        var game = RoadsTests.TwoFlags();
        var drawing = new RoadDrawing(game);
        drawing.Click(16, 6);
        drawing.Click(16, 7);

        // A click on the node a wrapped map's edge shares with the end counts as a step: the column 16 + 32 is 16 again.
        Assert.IsNull(drawing.Click(16 + 32, 8));
        Assert.AreEqual(3, drawing.Nodes.Count);
        Assert.AreEqual((16, 8), drawing.Nodes[^1]);
    }
}
