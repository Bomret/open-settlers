using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Tests of the forester's own logic and the saplings' growth on made-up tables (no install), in the world of WoodcuttingTests: the
// castle at 15,5 with its flag at 16,6, the forester's hut at 16,2 with its flag at 17,3 (slot 3), and the roads of that world. The
// forester is a settler without a job that the castle sends out, so a test finds him by his job. The trees are gone from the map; the
// terrain of the hut's reach is what a test makes it. The values the original gives are in ForesterTests.
[TestClass]
public sealed class ForestryTests
{
    private const int HutX = 16;
    private const int HutY = 2;

    // The nodes the forester draws from: the 128 entries of the spiral around his hut's node, where he stands while he searches.
    private static (int X, int Y)[] Reach() =>
        [
            .. Enumerable
                .Range(1, 128)
                .Select(entry =>
                    (
                        (HutX + MapGenerator.SpiralOffsets[entry].Dx) & 31,
                        (HutY + MapGenerator.SpiralOffsets[entry].Dy) & 31
                    )
                ),
        ];

    private static (int X, int Y) NorthWest((int X, int Y) node) => ((node.X - 1) & 31, (node.Y - 1) & 31);

    // The made-up workers: the forester's building asks for the forester's job with no tool.
    private static WorkerRequest?[] Workers()
    {
        var workers = new WorkerRequest?[RuleTables.BuildingTypeCount];
        workers[BuildingType.Forester.Code] = new WorkerRequest(Job.Forester, Good.None);

        return workers;
    }

    private static void SetMeadow(Map map, (int X, int Y) node, bool meadow)
    {
        map.At(node.X, node.Y).Upper = meadow ? Terrain.Lowland : new Terrain(0);
        map.At(node.X, node.Y).Lower = meadow ? Terrain.Lowland : new Terrain(0);
    }

    // The castle, the flags, the roads, and the forester's hut; every node of his reach and its north-west neighbour is meadow
    // (both triangles of the code 5), and no object stands there but what a test puts.
    private static Game World(Action<Map>? prepare, out Map map, BuildingType? type = null)
    {
        var hut = type ?? BuildingType.Forester;
        var made = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
        for (var index = 0; index < made.Count; index++)
        {
            if (made.At(index).Object.Kind is MapObjectKind.Tree or MapObjectKind.StoneHeap)
            {
                made.At(index).Object = MapObject.None;
            }
        }

        foreach (var node in Reach())
        {
            SetMeadow(made, node, true);
            SetMeadow(made, NorthWest(node), true);
        }

        map = made;

        var game = GameFactory.Create(WoodcuttingTests.Made(hut == BuildingType.Forester ? Workers() : null), made);
        game.Enqueue(new Command(0, 0, CommandKind.PlaceCastle, 15, 5));
        game.Tick();
        foreach (var (x, y) in new[] { (18, 6), (19, 4), (17, 3) })
        {
            game.Enqueue(new Command(game.TickCount, 0, CommandKind.PlaceFlag, x, y));
            game.Tick();
        }

        foreach (
            var (x, y, directions) in new[]
            {
                (16, 6, new[] { 0, 0 }),
                (18, 6, new[] { 5, 5, 0 }),
                (17, 3, new[] { 0, 1 }),
            }
        )
        {
            game.Enqueue(new Command(game.TickCount, 0, CommandKind.BuildRoad, x, y, directions));
            game.Tick();
        }

        Huts.PlaceAndTick(game, HutX, HutY, hut);
        prepare?.Invoke(made);

        return game;
    }

    // Whether the node holds nothing of the world's own: no object, no road, no lake mark.
    private static bool Bare(Map map, (int X, int Y) node) =>
        !map.At(node.X, node.Y).Object.Exists
        && map.At(node.X, node.Y).Roads.IsEmpty
        && !map.At(node.X, node.Y).LakeMark;

    private static Settler Forester(Game game) => game.Settlers.Single(settler => settler.Job == Job.Forester);

    private static (int X, int Y)[] Saplings(Map map) =>
        [.. Reach().Where(node => map.At(node.X, node.Y).Object.Kind == MapObjectKind.Sapling)];

    private static void RunUntilPlanted(Game game, Map map, int ticks = 12000)
    {
        for (var tick = 0; tick < ticks && Saplings(map).Length == 0; tick++)
        {
            game.Tick();
        }
    }

    [TestMethod]
    public void AForesterPlantsASaplingOnAFreeMeadowNodeOfHisReachAndWalksHome()
    {
        var game = World(null, out var map);

        RunUntilPlanted(game, map);

        var saplings = Saplings(map);
        Assert.HasCount(1, saplings, "one sapling stands in his reach");
        Assert.IsTrue(map.At(saplings[0].X, saplings[0].Y).Object.Code is 0x67 or 0x68, "a small sapling's code");
        for (var tick = 0; tick < 400 && Forester(game).Activity is not WalksFree { Trip.Purpose: BackToHut }; tick++)
        {
            game.Tick();
        }

        Assert.IsTrue(
            Forester(game).Activity is WalksFree { Trip.Purpose: BackToHut { Carrying: false } },
            "he walks back to the hut's flag, carrying nothing"
        );
    }

    [TestMethod]
    [DataRow("object")]
    [DataRow("flag")]
    [DataRow("road")]
    [DataRow("lake")]
    [DataRow("terrain")]
    [DataRow("neighbour")]
    public void AForesterNeverPlantsWhereTheOriginalRefuses(string refusal)
    {
        var reach = Reach();
        // The one node the forester may plant on (with its north-west neighbour, which the terrain test needs as meadow): an
        // entry whose north-west neighbour lies outside the reach. Where the refusal is a lake mark, which stops his walk where
        // it stands thick, the nodes of the even columns and rows are the refused ones and the others are free.
        var valid = reach.Last(node => !reach.Contains(NorthWest(node)));
        Func<(int X, int Y), bool> refused =
            refusal == "lake" ? node => node.X % 2 == 0 && node.Y % 2 == 0 : node => node != valid;
        var game = World(
            map =>
            {
                foreach (var node in reach.Where(node => refused(node) && Bare(map, node)))
                {
                    ref var at = ref map.At(node.X, node.Y);
                    switch (refusal)
                    {
                        case "object":
                            at.Object = new MapObject(0x4a);
                            break;
                        case "flag":
                            at.Object = MapObject.Flag;
                            at.FlagMark = true;
                            break;
                        case "road":
                            at.Roads = RoadSet.Of(0);
                            break;
                        case "lake":
                            at.LakeMark = true;
                            break;
                        case "terrain":
                            SetMeadow(map, node, false);
                            break;
                        default:
                            // A node of an even column is meadow, and the node north-west of it, in an odd column, is not.
                            SetMeadow(map, node, false);
                            SetMeadow(map, NorthWest(node), false);
                            break;
                    }
                }

                if (refusal == "neighbour")
                {
                    // The nodes of the even columns are meadow too, and each has a north-west neighbour in an odd column.
                    foreach (var node in reach.Where(node => node.X % 2 == 0))
                    {
                        SetMeadow(map, node, true);
                    }
                }

                SetMeadow(map, valid, true);
                SetMeadow(map, NorthWest(valid), true);
            },
            out var map
        );

        RunUntilPlanted(game, map, 40000);

        var saplings = Saplings(map);
        Assert.IsNotEmpty(saplings, "he plants where he may");
        Assert.IsFalse(saplings.Any(refused), $"refusal {refusal}: no sapling on a refused node");
    }

    [TestMethod]
    public void WithNoFreeNodeInReachTheForesterWaitsInHisHutAndTheGameKeepsTicking()
    {
        var game = World(
            map =>
            {
                foreach (var node in Reach().Where(node => Bare(map, node)))
                {
                    map.At(node.X, node.Y).Object = new MapObject(0x4a);
                }
            },
            out var map
        );

        for (var tick = 0; tick < 3000; tick++)
        {
            game.Tick();
        }

        var digest = game.Digest();
        var ticks = game.TickCount;
        game.Tick();

        Assert.IsInstanceOfType<LooksForSpot>(Forester(game).Activity, "he looks for a node inside the hut");
        Assert.AreEqual((HutX, HutY), (Forester(game).X, Forester(game).Y));
        Assert.IsEmpty(Saplings(map));
        Assert.AreEqual(ticks + 1, game.TickCount);
        Assert.AreNotEqual(digest, game.Digest(), "the game state moves on");
    }

    [TestMethod]
    public void ASaplingGrowsIntoATreeAndTheWoodcutterFellsItAndLaysALogOnHisFlag()
    {
        var saplings = new List<(int X, int Y)>();
        var game = World(
            map =>
            {
                // A sapling stands on the nodes of the woodcutter's reach whose column plus twice the row is a multiple of 7.
                foreach (var node in Reach().Where(node => (node.X + 2 * node.Y) % 7 == 0 && Bare(map, node)))
                {
                    map.At(node.X, node.Y).Object = new MapObject((byte)(MapObject.SaplingCode + node.X % 2));
                    saplings.Add(node);
                }
            },
            out var map,
            BuildingType.Woodcutter
        );
        var grown = false;
        var log = false;

        for (var tick = 0; tick < 120000 && !log; tick++)
        {
            game.Tick();
            grown |= saplings.Any(node => map.At(node.X, node.Y).Object.Kind != MapObjectKind.Sapling);
            log = game.Flags[3].Places.Any(place => place.Good == new Good(WoodcutterBehavior.GoodLog));
        }

        Assert.IsTrue(grown, "a sapling of the hut's reach grew");
        Assert.IsTrue(log, "the woodcutter felled a grown tree and laid its log on the flag");
    }

    // The map's object pass with a sapling at the first node its cursor reaches (the column 23 of row 0), and the draws given.
    private static (MapObject After, int Taken) Grow(byte sapling, params ushort[] draws)
    {
        var bench = new BehaviourBench(draws);
        var behaviour = new WoodcutterBehavior(
            bench.Movement,
            bench.Settlers,
            bench.Flags,
            bench.History,
            bench.Draws,
            new WalkHomeBehavior(
                bench.Movement,
                bench.Buildings,
                bench.Flags,
                bench.Roads,
                bench.History,
                bench.Draws,
                (_, _) => -1
            ),
            (_, _, _) => { },
            () => false
        );
        bench.Map.At(23, 0).Object = new MapObject(sapling);
        bench.Clock = 8 * 40;

        behaviour.ObjectPass();

        return (bench.Map.At(23, 0).Object, bench.Draws.Taken);
    }

    [TestMethod]
    public void TheSaplingOfTheCode0x67GrowsIntoATreeOfTheCodes0x10To0x17WhereTheDrawsBits8And9AreClear()
    {
        var (after, taken) = Grow(0x67, 0x0005);

        Assert.AreEqual((0x15, MapObjectKind.Tree, 1), (after.Code, after.Kind, taken));
    }

    [TestMethod]
    public void TheSaplingOfTheCode0x68GrowsIntoATreeOfTheCodes8To0xf()
    {
        var (after, taken) = Grow(0x68, 0x0005);

        Assert.AreEqual((0x0d, MapObjectKind.Tree, 1), (after.Code, after.Kind, taken));
    }

    [TestMethod]
    [DataRow((byte)0x67)]
    [DataRow((byte)0x68)]
    public void ASaplingStaysWhereTheDrawsBits8Or9AreSet(byte sapling)
    {
        var (after, taken) = Grow(sapling, 0x0105);

        Assert.AreEqual((sapling, MapObjectKind.Sapling, 1), (after.Code, after.Kind, taken));
    }
}
