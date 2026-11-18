using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The road cursor's verdicts of a game (docs/rules/roads.md, "The allowed directions"). The verdicts of a map alone are the
// tests of the class of the same name in the application's test project.
[TestClass]
public sealed class RoadCursorTests
{
    static readonly RoadCursorVerdict[] AllBlocked = [.. Enumerable.Repeat(RoadCursorVerdict.Blocked, 6)];

    [TestMethod]
    public void JudgingTheVerdictsLeavesTheMapAndTheGameUnchanged()
    {
        var game = RoadWorld.TwoFlags();
        var digest = game.Digest();
        var roads = Enumerable.Range(0, game.Map.Count).Select(index => game.Map.At(index).Roads).ToArray();

        game.RoadPlanner.Verdicts(16, 6, [0]);

        Assert.AreEqual(digest, game.Digest());
        CollectionAssert.AreEqual(
            roads,
            Enumerable.Range(0, game.Map.Count).Select(index => game.Map.At(index).Roads).ToArray()
        );
    }

    [TestMethod]
    public void BeforeTheCastleStandsEveryDirectionIsBlocked()
    {
        var map = new Map(MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            map.At(index).Owner = Owner.Player(0);
            map.At(index).Upper = Terrain.Lowland;
            map.At(index).Lower = Terrain.Lowland;
        }

        map.At(10, 10).Object = MapObject.Flag;
        var game = GameFactory.Create(MadeProgram.Made(), map);

        CollectionAssert.AreEqual(AllBlocked, game.RoadPlanner.Verdicts(10, 10, []).ToArray());
    }
}
