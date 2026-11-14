using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

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

    // The direction each heading class (0x437072) takes first, as 4 times its number plus 4: a step that cuts the vector
    // the class describes. The classes 12 to 15 point west and north, 16 and 17 west and south, 18 to 21 east and south,
    // 22 and 23 east and north.
    static readonly byte[] FirstDirections = [20, 20, 20, 20, 16, 12, 12, 8, 8, 4, 4, 24];

    internal static RuleTables Made(WorkerRequest?[]? workers = null)
    {
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        var rows = new byte[RuleTables.WalkRowCount * RuleTables.WalkRowSize];
        for (var row = 0; row < RuleTables.WalkRowCount; row++)
        {
            // The six directions, the heading class's own first and the others after it.
            var first = row < 12 ? (byte)4 : FirstDirections[row - 12];
            var order = new[] { first }.Concat(new byte[] { 4, 8, 12, 16, 20, 24 }.Where(code => code != first));
            order.ToArray().CopyTo(rows, row * RuleTables.WalkRowSize);
        }

        return new RuleTables(
            classes,
            [
                .. Enumerable
                    .Range(0, RuleTables.StartStockRows)
                    .Select(row =>
                        Enumerable.Range(0, StartStock.Goods).Select(good => (byte)(row * 10 + good)).ToArray()
                    ),
            ],
            [.. Enumerable.Repeat((ushort)100, RuleTables.StepTimeCount)],
            [.. Enumerable.Repeat((byte)8, RuleTables.DoorSpeedCount)],
            [.. Enumerable.Repeat((ushort)16, RuleTables.ScanWindowCount)],
            walkRows: rows,
            workers: workers ?? TestProgram.MadeWorkers()
        );
    }

    // The 128 nodes around the hut the worker draws from, as the spiral gives them.
    static bool HoldsLog(Game game) => game.Flags[HutFlag].Places.Any(place => place.Good == new Good { Code = 7 });

    // The goods on the hut's flag, place by place.
    static Good[] Goods(Game game) => [.. game.Flags[HutFlag].Places.Select(place => place.Good)];

    static (int X, int Y)[] Reach() =>
        [
            .. Enumerable
                .Range(1, 128)
                .Select(entry => (16 + MapGenerator.SpiralOffsets[entry].Dx, 2 + MapGenerator.SpiralOffsets[entry].Dy)),
        ];

    static bool IsTree(Map map, (int X, int Y) node) => map.At(node.X, node.Y).Object.Kind == MapObjectKind.Tree;

    // The castle, the flags, the roads, and the hut; a tree stands on every node of the hut's reach where trees is true,
    // and the generated trees are gone from the whole map.
    internal static Game World(bool trees, out Map map)
    {
        map = MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        for (var index = 0; index < map.Count; index++)
        {
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        if (trees)
        {
            foreach (var (x, y) in Reach())
            {
                var index = map.Index(x & 31, y & 31);
                if (!map.At(index).Object.Exists)
                {
                    map.At(index).Object = new MapObject { Code = 8 };
                }
            }
        }

        var game = GameFactory.Create(Made(), map);
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            game.Enqueue(
                new Command
                {
                    Tick = game.TickCount,
                    Player = 0,
                    Kind = CommandKind.PlaceFlag,
                    X = x,
                    Y = y,
                }
            );
            game.Tick();
        }

        foreach (var (x, y, directions) in new[] { (16, 6, new[] { 0, 0 }), (18, 6, [5, 5, 0]), (17, 3, [0, 1]) })
        {
            game.Enqueue(
                new Command
                {
                    Tick = game.TickCount,
                    Player = 0,
                    Kind = CommandKind.BuildRoad,
                    X = x,
                    Y = y,
                    Directions = directions,
                }
            );
            game.Tick();
        }

        Huts.PlaceAndTick(game, 16, 2);

        return game;
    }

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

        Assert.AreEqual(
            new Good { Code = 7 },
            game.Flags[HutFlag].Places[0].Good,
            "the log lies on the first place of the flag"
        );
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
    public void TheObjectLayerWatchReportsEveryTickThatChangesTheObjectLayerOrTheGoodsOnAFlag()
    {
        var game = World(true, out var map);
        var watch = new OpenSettlers.MapView.ObjectLayerWatch();
        Assert.IsTrue(watch.Changed(game), "the first look is a change: nothing was drawn yet");
        var objects = TestMaps.Objects(map);
        var goods = Goods(game);
        var changes = 0;

        for (var tick = 0; tick < 6000 && !HoldsLog(game); tick++)
        {
            game.Tick();
            var now = TestMaps.Objects(map);
            var moved = !now.SequenceEqual(objects) || !Goods(game).SequenceEqual(goods);
            Assert.AreEqual(moved, watch.Changed(game), $"tick {game.TickCount}");
            changes += moved ? 1 : 0;
            objects = now;
            goods = Goods(game);
        }

        Assert.IsGreaterThan(1, changes, "a tree is cut and a log laid");
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
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        var game = GameFactory.Create(Made(), map);
        Assert.AreEqual(
            map.Generator,
            game.Draws.Generator,
            "the game's generator starts where the map's generation ended"
        );
        var same = GameFactory.Create(
            Made(),
            MapGenerator.Generate(
                TestProgram.Made(),
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
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        other.Generator = other.Generator.Step();

        Assert.AreNotEqual(game.Digest(), GameFactory.Create(Made(), other).Digest());
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
