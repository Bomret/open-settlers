using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;
using static OpenSettlers.Adapters.Testing.WoodcuttingWorld;

namespace OpenSettlers.Tests;

// The woodcutter's work seen from the view (the worker itself is in the adapters' WoodcuttingTests), on made-up tables (no
// install). The map of the seed 1234,5678,9ABC has its castle at 15,5 with its flag at 16,6; the hut stands north-west of the
// flag at 17,3 (16,2), and roads run from the castle's flag east to 18,6, from there north and east to 19,4, and from 17,3
// east and south-east to 19,4. A tree stands on every node within five of the hut where trees is true.
[TestClass]
public sealed class WoodcuttingTests
{
    const int HutFlag = 3;

    static bool HoldsLog(Game game) => game.Flags[HutFlag].Places.Any(place => place.Good == Good.Log);

    // The goods on the hut's flag, place by place.
    static Good[] Goods(Game game) => [.. game.Flags[HutFlag].Places.Select(place => place.Good)];

    static (bool Constructing, ushort Progress) Castle(Game game) =>
        game.Buildings[0].Constructing ? (true, game.Buildings[0].Progress) : (false, (ushort)0);

    [TestMethod]
    public void TheObjectLayerWatchReportsEveryTickThatChangesTheObjectLayerOrTheGoodsOnAFlag()
    {
        var game = World(true, out var map);
        var watch = new OpenSettlers.MapView.ObjectLayerWatch();
        Assert.IsTrue(watch.Changed(game), "the first look is a change: nothing was drawn yet");
        var objects = TestMaps.Objects(map);
        var goods = Goods(game);
        var castle = Castle(game);
        var changes = 0;

        for (var tick = 0; tick < 6000 && !HoldsLog(game); tick++)
        {
            game.Tick();
            var now = TestMaps.Objects(map);
            // The castle under construction is drawn by its progress word, so its moves and the pass it stands change the layer too.
            var moved = !now.SequenceEqual(objects) || !Goods(game).SequenceEqual(goods) || Castle(game) != castle;
            Assert.AreEqual(moved, watch.Changed(game), $"tick {game.TickCount}");
            changes += moved ? 1 : 0;
            objects = now;
            goods = Goods(game);
            castle = Castle(game);
        }

        Assert.IsGreaterThan(1, changes, "a tree is cut and a log laid");
    }

    [TestMethod]
    public void TheObjectLayerWatchNoticesAGoodWithNoCodeArrivingOnAndLeavingAFlag()
    {
        var game = World(false, out _);
        var watch = new OpenSettlers.MapView.ObjectLayerWatch();
        game.Tick();
        Assert.IsTrue(watch.Changed(game));
        game.Tick();
        Assert.IsFalse(watch.Changed(game), "nothing moves with no tree in reach");

        game.LayGood(0, 7, new Good { Name = "wine" });
        game.Tick();
        Assert.IsTrue(watch.Changed(game), "a good with no code arrived");
        game.Tick();
        Assert.IsFalse(watch.Changed(game));

        game.LayGood(0, 7, new Good { Name = "ale" });
        game.Tick();
        Assert.IsTrue(watch.Changed(game), "another good with no code took its place");
        game.Tick();
        Assert.IsFalse(watch.Changed(game));

        game.LayGood(0, 7, Good.None);
        game.Tick();
        Assert.IsTrue(watch.Changed(game), "the good with no code left");
    }

    [TestMethod]
    public void TheObjectLayerWatchLooksOnlyAfterATickRan()
    {
        var game = World(true, out var map);
        var watch = new OpenSettlers.MapView.ObjectLayerWatch();
        Assert.IsTrue(watch.Changed(game));

        // Only a tick changes the game, so a look between two ticks does not read the layers again.
        map.At(0).Object = new MapObject { Code = (byte)(map.At(0).Object.Code ^ 0x7f) };

        Assert.IsFalse(watch.Changed(game));
    }
}
