using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A road that ends at the flag it started from (docs/rules/roads.md), taken apart by the removal's take-down of a flag: the bench's
// game on its flat map with the castle at 15,5 and its flag at 16,6 (slot 0), on the removal's made-up tables. The loop leaves the
// castle's flag east around the hexagon of the node 17,7 and returns by a last step of direction 5; a flag on the opposite corner,
// 18,8, splits it into two roads between the same two flags. The rest of the class is in src/Adapters.Tests.
[TestClass]
public sealed class RoadLoopTests
{
    static readonly int[] Loop = [0, 1, 2, 3, 4, 5];

    [TestMethod]
    public void TakingDownTheFlagThatSplitTheLoopJoinsItsHalvesBackIntoTheLoop()
    {
        // The original's click refuses this flag, so the join is reached through the take-down itself.
        var game = GameBench.Create(MadeProgram.RemovalTables(), GameBench.Flat());
        GameBench.Do(game, CommandKind.PlaceCastle, 15, 5);
        GameBench.Do(game, CommandKind.BuildRoad, 16, 6, Loop);
        GameBench.Do(game, CommandKind.PlaceFlag, 18, 8);
        Assert.HasCount(2, game.Network.Roads, "the flag splits the loop");

        game.Removal.TakeFlagDown(game.Map.Index(18, 8));

        var road = game.Network.Roads.Single();
        Assert.AreEqual((0, 0), (road.FromSlot, road.ToSlot));
        Assert.AreEqual(Loop.Length, road.Directions.Count);
        CollectionAssert.AreEquivalent(
            new[] { 0, 2 },
            new[] { road.FromDirection.Value, road.ToDirection.Value },
            "the joined road starts at either end"
        );
        Assert.AreEqual(new LengthClass { Value = 2 }, game.Flags[0].Side(0).Length);
    }
}
