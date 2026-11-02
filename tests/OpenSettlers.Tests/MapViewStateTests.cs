using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class MapViewStateTests
{
    const string Seed = "1234567812345678";

    [TestMethod]
    public void OpeningGeneratesTheMapOfTheGivenSeedAndSize()
    {
        var state = MapViewState.Open(Seed, 1);

        Assert.IsNotNull(state.Map);
        Assert.AreEqual(32, state.Map.Size.Width);
        Assert.IsNull(state.Problem);
        Assert.AreEqual(Seed, state.SeedText);
    }

    [TestMethod]
    public void AValidNewSeedRegeneratesTheMap()
    {
        var state = MapViewState.Open(Seed, 1);
        var first = state.Map;

        var changed = state.Apply("8765432187654321", 1);

        Assert.IsTrue(changed);
        Assert.AreNotSame(first, state.Map);
        Assert.AreNotEqual(first!.Digest(MapLayer.Height), state.Map!.Digest(MapLayer.Height));
    }

    [TestMethod]
    public void ANewSizeRegeneratesTheMap()
    {
        var state = MapViewState.Open(Seed, 1);

        var changed = state.Apply(Seed, 2);

        Assert.IsTrue(changed);
        Assert.AreEqual(64, state.Map!.Size.Width);
        Assert.AreEqual(2, state.SizeValue);
    }

    [TestMethod]
    public void TheSameSeedAndSizeKeepTheMap()
    {
        var state = MapViewState.Open(Seed, 1);
        var first = state.Map;

        Assert.IsFalse(state.Apply(Seed, 1));
        Assert.AreSame(first, state.Map);
    }

    [TestMethod]
    public void AnInvalidSeedShowsItsReasonAndKeepsTheLastMap()
    {
        var state = MapViewState.Open(Seed, 1);
        var first = state.Map;

        var changed = state.Apply("12345678123456", 2);

        Assert.IsFalse(changed);
        Assert.AreSame(first, state.Map);
        StringAssert.Contains(state.Problem, "14 characters");
        Assert.AreEqual(1, state.SizeValue);
        Assert.AreEqual(Seed, state.SeedText);
    }

    [TestMethod]
    public void AValidChangeClearsTheProblem()
    {
        var state = MapViewState.Open(Seed, 1);
        state.Apply("9", 1);

        state.Apply("8765432187654321", 1);

        Assert.IsNull(state.Problem);
    }

    [TestMethod]
    public void ASizeTheOriginalDoesNotOfferKeepsTheLastMap()
    {
        var state = MapViewState.Open(Seed, 1);
        var first = state.Map;

        Assert.IsFalse(state.Apply(Seed, 9));
        Assert.AreSame(first, state.Map);
        Assert.IsNotNull(state.Problem);
    }

    [TestMethod]
    public void OpeningWithAnInvalidSeedHasNoMapAndAReason()
    {
        var state = MapViewState.Open("0000000000000000", 1);

        Assert.IsNull(state.Map);
        StringAssert.Contains(state.Problem, "'0'");
    }
}
