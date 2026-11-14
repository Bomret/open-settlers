using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The state of a thing is its fields in order (ThingState): the lookup of a field and the equality of two states.
[TestClass]
public sealed class ThingStateTests
{
    [TestMethod]
    public void AFieldTheStateDoesNotListAnswersNull()
    {
        var state = new ThingState { Fields = [("x", 4)] };

        Assert.IsNull(state.Get("y"));
    }

    [TestMethod]
    public void AFieldListedTwiceAnswersItsFirstValue()
    {
        var state = new ThingState { Fields = [("x", 4), ("y", 1), ("x", 9)] };

        Assert.AreEqual(4, state.Get("x"));
    }

    [TestMethod]
    public void StatesWithTheSameFieldsInTheSameOrderAreEqualWithEqualHashes()
    {
        var one = new ThingState { Fields = [("x", 4), ("y", 1)] };
        var other = new ThingState { Fields = [("x", 4), ("y", 1)] };

        Assert.AreEqual(one, other);
        Assert.AreEqual(one.GetHashCode(), other.GetHashCode());
    }

    [TestMethod]
    public void StatesDifferWhereAValueDiffers()
    {
        Assert.AreNotEqual(
            new ThingState { Fields = [("x", 4), ("y", 1)] },
            new ThingState { Fields = [("x", 4), ("y", 2)] }
        );
    }

    [TestMethod]
    public void StatesDifferWhereTheOrderDiffers()
    {
        Assert.AreNotEqual(
            new ThingState { Fields = [("x", 4), ("y", 1)] },
            new ThingState { Fields = [("y", 1), ("x", 4)] }
        );
    }
}
