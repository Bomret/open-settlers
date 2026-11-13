using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ViewMessageTests
{
    // Settlers that cannot be drawn (their step offsets or sprites did not load) are named in the view, as the objects
    // and the roads are, and not left out of the game view without a word.
    [TestMethod]
    public void ASettlersProblemIsShownInTheView()
    {
        Assert.AreEqual(
            "Cannot draw the settlers: no entry 2",
            ViewMessage.Of(graphics: null, state: null, objects: null, roads: null, settlers: "no entry 2")
        );
    }

    // The wording of each line is the view's own (src/MapView/ViewMessage.cs): a prefix for each part but the state, which shows as is.
    [TestMethod]
    public void AGraphicsProblemIsShownWithItsPrefix() =>
        Assert.AreEqual(
            "Cannot draw the map: no archive",
            ViewMessage.Of("no archive", state: null, objects: null, roads: null, settlers: null)
        );

    [TestMethod]
    public void AStateProblemIsShownAsItIs() =>
        Assert.AreEqual(
            "Loading",
            ViewMessage.Of(graphics: null, "Loading", objects: null, roads: null, settlers: null)
        );

    [TestMethod]
    public void AnObjectsProblemIsShownWithItsPrefix() =>
        Assert.AreEqual(
            "Cannot draw the objects: no entry 3",
            ViewMessage.Of(graphics: null, state: null, "no entry 3", roads: null, settlers: null)
        );

    [TestMethod]
    public void ARoadsProblemIsShownWithItsPrefix() =>
        Assert.AreEqual(
            "Cannot draw the roads: no entry 4",
            ViewMessage.Of(graphics: null, state: null, objects: null, "no entry 4", settlers: null)
        );

    [TestMethod]
    public void NoProblemGivesAnEmptyLine() => Assert.AreEqual("", ViewMessage.Of(null, null, null, null, null));

    // The order the summary of the type states: the graphics, the state, the objects, the roads, the settlers.
    [TestMethod]
    public void TheFirstProblemInOrderWins()
    {
        Assert.AreEqual("Cannot draw the map: g", ViewMessage.Of("g", "s", "o", "r", "t"));
        Assert.AreEqual("s", ViewMessage.Of(null, "s", "o", "r", "t"));
        Assert.AreEqual("Cannot draw the objects: o", ViewMessage.Of(null, null, "o", "r", "t"));
        Assert.AreEqual("Cannot draw the roads: r", ViewMessage.Of(null, null, null, "r", "t"));
    }
}
