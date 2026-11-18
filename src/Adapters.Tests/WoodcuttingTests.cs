using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;
using static OpenSettlers.Adapters.Testing.WoodcuttingWorld;

namespace OpenSettlers.Tests;

// Tests of the woodcutter's own logic on made-up tables (no install): every step takes 100 clock units, the doors have one
// speed, every object class is 0 but the flag's, and the walk's direction rows hold, as their first direction, a step that
// takes a vector nearer to 0. The values the original gives are in WoodcutterTests. The map of the seed
// 1234,5678,9ABC has its castle at 15,5 with its flag at 16,6; the hut stands north-west of the flag at 17,3 (16,2), and
// roads run from the castle's flag east to 18,6, from there north and east to 19,4, and from 17,3 east and south-east to 19,4.
[TestClass]
public sealed class WoodcuttingTests
{
    const int HutFlag = 3;

    // The 128 nodes around the hut the worker draws from, as the spiral gives them.
    static bool HoldsLog(Game game) => game.Flags[HutFlag].Places.Any(place => place.Good == Good.Log);

    // The goods on the hut's flag, place by place.
    static Good[] Goods(Game game) => [.. game.Flags[HutFlag].Places.Select(place => place.Good)];

    static bool IsTree(Map map, (int X, int Y) node) => map.At(node.X, node.Y).Object.Kind == MapObjectKind.Tree;

    [TestMethod]
    public void AWorkerCutsATreeOfItsReachAndLaysALogOnTheHutsFlag()
    {
        var game = World(true, out var map);
        var reach = Reach();
        var before = reach.Count(node => IsTree(map, node));

        for (var tick = 0; tick < 6000 && !HoldsLog(game); tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(Good.Log, game.Flags[HutFlag].Places[0].Good, "the log lies on the first place of the flag");
        Assert.AreEqual(8, game.Flags[HutFlag].Places.Count);
        Assert.AreEqual(before - 1, reach.Count(node => IsTree(map, node)), "one tree is felled");
    }

    [TestMethod]
    public void AFelledTreeIsGoneFromTheMapInTheEnd()
    {
        var game = World(true, out var map);
        var reach = Reach();
        (int X, int Y)? felled = null;

        for (var tick = 0; tick < 60000; tick++)
        {
            game.Tick();
            felled ??= reach
                .Cast<(int X, int Y)?>()
                .FirstOrDefault(node => map.At(node!.Value.X, node.Value.Y).Object.Kind == MapObjectKind.FallingTree);
            if (felled is { } node && !map.At(node.X, node.Y).Object.Exists)
            {
                return;
            }
        }

        Assert.Fail("the felled tree still stands on the map");
    }

    [TestMethod]
    public void WithNoTreeInReachTheWorkerWaitsInItsHutAndTheGameKeepsTicking()
    {
        var game = World(false, out var map);
        var objects = TestMaps.Objects(map);

        for (var tick = 0; tick < 3000; tick++)
        {
            game.Tick();
        }

        var digest = game.Digest();
        var ticks = game.TickCount;
        game.Tick();

        Assert.IsInstanceOfType<LooksForTree>(game.Settlers[10].Activity, "the worker looks for a tree inside the hut");
        Assert.AreEqual((16, 2), (game.Settlers[10].X, game.Settlers[10].Y));
        Assert.IsTrue(game.Flags[HutFlag].Places.All(place => place.IsEmpty));
        Assert.AreEqual(ticks + 1, game.TickCount);
        Assert.AreNotEqual(digest, game.Digest(), "the game state moves on");
        var after = TestMaps.Objects(map);
        Assert.IsTrue(
            objects.Zip(after).All(pair => pair.First == pair.Second || pair.First.Kind == MapObjectKind.Stump),
            "the object pass changes only stumps"
        );
    }

    [TestMethod]
    public void TheWorkersDrawsComeFromTheGamesGeneratorAndShowInTheDigest()
    {
        var map = MapGenerator.Generate(
            MadeProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        var game = GameFactory.Create(MadeProgram.WoodcuttingTables(), map);
        Assert.AreEqual(
            map.Generator,
            game.RandomState,
            "the game's generator starts where the map's generation ended"
        );
        var same = GameFactory.Create(
            MadeProgram.WoodcuttingTables(),
            MapGenerator.Generate(
                MadeProgram.Made(),
                new MapSeed
                {
                    W0 = 0x1234,
                    W1 = 0x5678,
                    W2 = 0x9ABC,
                },
                MapSize.Offered[0]
            )
        );
        Assert.AreEqual(game.Digest(), same.Digest());

        var other = MapGenerator.Generate(
            MadeProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        other.Generator = other.Generator.Step();

        Assert.AreNotEqual(game.Digest(), GameFactory.Create(MadeProgram.WoodcuttingTables(), other).Digest());
    }

    [TestMethod]
    public void TwoGamesWithTheSameCommandsCutTheSameTrees()
    {
        var first = World(true, out var firstMap);
        var second = World(true, out var secondMap);

        for (var tick = 0; tick < 3000; tick++)
        {
            first.Tick();
            second.Tick();
        }

        Assert.AreEqual(first.Digest(), second.Digest());
        CollectionAssert.AreEqual(TestMaps.Objects(firstMap), TestMaps.Objects(secondMap));
    }
}
