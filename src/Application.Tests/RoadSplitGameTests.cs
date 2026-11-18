using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The join of a road split by a flag (docs/rules/roads.md#a-flag-on-a-road) where the original's click refuses the flag, reached
// through the removal's take-down itself: the bench's game on its flat map and the removal's made-up tables. The castle at 15,5 has
// its flag at 16,6 (slot 0); a flag at 20,6 (slot 1) ends the road east of it, and the flag that splits the road goes to 18,6 (slot 2).
// The rest of the class is in src/Adapters.Tests.
[TestClass]
public sealed class RoadSplitGameTests
{
    // The road from the castle's flag east to the flag at 20,6; beforeRoad changes the map after the flags stand and before the road.
    static Game EastRoad(Action<Map>? beforeRoad = null)
    {
        var map = GameBench.Flat();
        var game = GameBench.Create(MadeProgram.RemovalTables(), map);
        GameBench.Do(game, CommandKind.PlaceCastle, 15, 5);
        GameBench.Do(game, CommandKind.PlaceFlag, 20, 6);
        beforeRoad?.Invoke(map);
        GameBench.Do(game, CommandKind.BuildRoad, 16, 6, [0, 0, 0, 0]);
        Assert.AreEqual(1, game.Network.Roads.Count, "the road is built");

        return game;
    }

    // A road as its two ends and its path, read from the lower slot: the joined road may start at either end.
    static string Describe(Road road) =>
        road.FromSlot <= road.ToSlot
            ? $"{road.FromSlot}.{road.FromDirection}>{road.ToSlot}.{road.ToDirection} {string.Concat(road.Directions)} {road.Water}"
            : $"{road.ToSlot}.{road.ToDirection}>{road.FromSlot}.{road.FromDirection} {string.Concat(road.Directions.Reverse().Select(step => (step + 3) % 6))} {road.Water}";

    [TestMethod]
    public void TwoWaterRoadsJoinIntoAWaterRoad()
    {
        // The triangles beside the steps from 16,6 to 20,6 are water (the lower triangle of a node and the upper of its north node,
        // nibbles below 4); the other triangles around the nodes stay land, so a flag is allowed on the road. The original's click
        // refuses a flag between water roads, so the join is reached through the take-down itself.
        var game = EastRoad(map =>
        {
            for (var x = 16; x < 20; x++)
            {
                map.At(x, 6).Lower = new Terrain { Code = map.At(x, 6).Lower.Code & 3 };
                map.At(x, 5).Upper = new Terrain { Code = map.At(x, 5).Upper.Code & 3 };
            }
        });
        GameBench.Do(game, CommandKind.PlaceFlag, 18, 6);

        game.Removal.TakeFlagDown(game.Map.Index(18, 6));

        Assert.AreEqual("0.0>1.3 0000 True", Describe(game.Network.Roads.Single()));
    }

    // The click refuses the flag while the hut stands (ClickClass), so the join is reached through the take-down itself.
    [TestMethod]
    public void TheMarkOfAStandingHutAtItsFlagIsNoRoadWhenTheFlagJoinsTheRoad()
    {
        var game = EastRoad();
        var before = game.Network.Roads.Single();
        Huts.PlaceAndTick(game, 17, 5);
        var node = game.Map.Index(18, 6);
        Assert.IsTrue(game.Map.At(node).Roads.Has(4), "the hut's mark is set");

        game.Removal.TakeFlagDown(node);

        Assert.AreEqual(Describe(before), Describe(game.Network.Roads.Single()));
    }
}
