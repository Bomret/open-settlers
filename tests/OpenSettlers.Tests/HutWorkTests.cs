using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The object pass of the woodcutter's hut work in a ticked game. The bench tests of the same class are in the
// application's test project.
[TestClass]
public sealed class HutWorkTests
{
    [TestMethod]
    public void TheGameRunsTheObjectPassOncePerPass()
    {
        const int Passes = 50;
        var game = StonecuttingWorld.World(null, out var map);
        map.At(9, 20).Object = MapObject.Falling(5, secondVariant: false);
        var runs = 0;
        var stamp = game.Hut.ObjectPassState.Stamp;

        for (var pass = 0; pass < Passes; pass++)
        {
            game.Tick();
            var after = game.Hut.ObjectPassState.Stamp;
            runs += after == stamp ? 0 : 1;
            stamp = after;
        }

        // The pass stamps the clock when it runs, and the clock moves on in every pass: a pass that skipped it leaves the stamp.
        Assert.AreEqual(Passes, runs);
    }

    [TestMethod]
    public void TheObjectPassOfTheGameReachesAFallingTree()
    {
        var game = StonecuttingWorld.World(null, out var map);
        map.At(9, 20).Object = MapObject.Falling(5, secondVariant: false);

        for (var pass = 0; pass < 40000 && map.At(9, 20).Object.Kind == MapObjectKind.FallingTree; pass++)
        {
            game.Tick();
        }

        Assert.AreNotEqual(MapObjectKind.FallingTree, map.At(9, 20).Object.Kind, "the pass reached the falling tree");
    }
}
