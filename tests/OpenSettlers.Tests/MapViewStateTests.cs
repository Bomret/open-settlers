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
        var state = MapViewState.Open(TestProgram.Made(), Seed, 1);

        Assert.IsNotNull(state.Map);
        Assert.AreEqual(32, state.Map.Size.Width);
        Assert.IsNull(state.Problem);
        Assert.AreEqual(Seed, state.SeedText);
    }

    [TestMethod]
    public void AValidNewSeedRegeneratesTheMap()
    {
        var state = MapViewState.Open(TestProgram.Made(), Seed, 1);
        var first = state.Map;

        var changed = state.Apply("8765432187654321", 1);

        Assert.IsTrue(changed);
        Assert.AreNotSame(first, state.Map);
        Assert.AreNotEqual(Layers.Digest(first!, MapLayer.Height), Layers.Digest(state.Map!, MapLayer.Height));
    }

    [TestMethod]
    public void TheAppliedSeedTypedInGroupsDoesNotRegenerate()
    {
        var state = MapViewState.Open(TestProgram.Made(), Seed, 1);

        var changed = state.Apply("1234 5678 1234 5678", 1);

        Assert.IsFalse(changed);
    }

    [TestMethod]
    public void ANewSizeRegeneratesTheMap()
    {
        var state = MapViewState.Open(TestProgram.Made(), Seed, 1);

        var changed = state.Apply(Seed, 2);

        Assert.IsTrue(changed);
        Assert.AreEqual(64, state.Map!.Size.Width);
        Assert.AreEqual(2, state.SizeValue);
    }

    [TestMethod]
    public void TheSameSeedAndSizeKeepTheMap()
    {
        var state = MapViewState.Open(TestProgram.Made(), Seed, 1);
        var first = state.Map;

        Assert.IsFalse(state.Apply(Seed, 1));
        Assert.AreSame(first, state.Map);
    }

    [TestMethod]
    public void AnInvalidSeedShowsItsReasonAndKeepsTheLastMap()
    {
        var state = MapViewState.Open(TestProgram.Made(), Seed, 1);
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
        var state = MapViewState.Open(TestProgram.Made(), Seed, 1);
        state.Apply("9", 1);

        state.Apply("8765432187654321", 1);

        Assert.IsNull(state.Problem);
    }

    [TestMethod]
    public void ASizeTheOriginalDoesNotOfferKeepsTheLastMap()
    {
        var state = MapViewState.Open(TestProgram.Made(), Seed, 1);
        var first = state.Map;

        Assert.IsFalse(state.Apply(Seed, 9));
        Assert.AreSame(first, state.Map);
        Assert.IsNotNull(state.Problem);
    }

    [TestMethod]
    public void TheStockLevelIsThatOfTheAppliedSeed()
    {
        var state = MapViewState.Open(TestProgram.Made(), Seed, 1);

        // The screen seed 1234567812345678 is the free-game seed 3905,0577,7739, whose level the emulator gave as 2
        // (opponents:3905,0577,7739).
        Assert.AreEqual(2, state.StockLevel);
    }

    [TestMethod]
    public void OpeningWithAnInvalidSeedHasNoMapAndAReason()
    {
        var state = MapViewState.Open(TestProgram.Made(), "0000000000000000", 1);

        Assert.IsNull(state.Map);
        StringAssert.Contains(state.Problem, "'0'");
    }

    [TestMethod]
    public void OpeningOnALayoutHoldsTheMapItDrawsAndItsSeed()
    {
        var layout = Layout.Read(LayoutTests.Text(objects: """[{"x": 7, "y": 2, "code": 8}]""")).Layout!;

        var state = MapViewState.OpenLayout(layout);

        Assert.AreEqual(Layers.Digest(layout.BuildMap(), MapLayer.Object), Layers.Digest(state.Map!, MapLayer.Object));
        Assert.AreEqual(layout.Seed.ToScreen(), state.SeedText);
        Assert.AreEqual(1, state.SizeValue);
        Assert.AreEqual(StartStock.LevelOf(layout.Seed), state.StockLevel);
        Assert.IsNull(state.Problem);
    }

    [TestMethod]
    public void ALayoutStateRefusesANewSeedAndKeepsItsMap()
    {
        var state = MapViewState.OpenLayout(Layout.Read(LayoutTests.Text()).Layout!);
        var map = state.Map;

        Assert.IsFalse(state.Apply("8765432187654321", 2));

        Assert.AreSame(map, state.Map);
        StringAssert.Contains(state.Problem, "layout");
    }

    [TestMethod]
    public void WithoutTablesTheStateNamesTheProblemAndHoldsNoMap()
    {
        var state = MapViewState.Open(null, Seed, 1, "Siedler1.exe is missing");

        Assert.IsNull(state.Map);
        StringAssert.Contains(state.Problem, "Siedler1.exe is missing");
        Assert.IsFalse(state.Apply("0000000000000001", 2));
        Assert.IsNull(state.Map);
        StringAssert.Contains(state.Problem, "Siedler1.exe is missing");
    }
}
