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
}
