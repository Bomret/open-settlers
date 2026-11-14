using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Tests of the stonecutter's own logic on made-up tables (no install), in the world of WoodcuttingTests: the castle at 15,5 with
// its flag at 16,6, the stonecutter's hut at 16,2 with its flag at 17,3 (slot 3), and the roads of that world. The generated
// trees and heaps are gone from the map; a heap stands where a test puts it. The castle's stonecutter is the settler of slot 12.
// The values the original gives are in StonecutterTests.
[TestClass]
public sealed class StonecuttingTests
{
    const int HutFlag = 3;
    const int WorkerSlot = 12;
    const int HutX = 16;
    const int HutY = 2;

    static readonly Good Stone = new() { Code = 10 };

    // The nodes a heap can stand on for the stonecutter: the north-west neighbour of each of the 128 nodes around the hut.
    static (int X, int Y)[] Reach() =>
        [
            .. Enumerable
                .Range(1, 128)
                .Select(entry =>
                    (
                        (HutX + MapGenerator.SpiralOffsets[entry].Dx - 1) & 31,
                        (HutY + MapGenerator.SpiralOffsets[entry].Dy - 1) & 31
                    )
                ),
        ];

    // The made-up workers: the stonecutter's building asks for the stonecutter's job with no tool.
    static WorkerRequest?[] Workers()
    {
        var workers = new WorkerRequest?[RuleTables.BuildingTypeCount];
        workers[BuildingType.Stonecutter.Code] = new WorkerRequest { Job = new Stonecutter(), Tool = Good.None };

        return workers;
    }

    static int Heaps(Map map) => Reach().Count(node => map.At(node.X, node.Y).Object.Kind == MapObjectKind.StoneHeap);

    // A heap stands on the nodes of his reach whose column plus twice the row is a multiple of 7: none beside another and none on
    // the node beside a heap, so the nodes he walks to and steps back to are free.
    static bool HeapNode((int X, int Y) node) => (node.X + 2 * node.Y) % 7 == 0;

    // The castle, the flags, the roads, and the stonecutter's hut; a heap of the code stands on each such free node of his reach
    // where a code is given, and the generated trees and heaps are gone from the whole map.
    internal static Game World(byte? heap, out Map map, RuleTables? tables = null, bool everyPass = false)
    {
        var made = MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        for (var index = 0; index < made.Count; index++)
        {
            if (made.At(index).Object.Kind is MapObjectKind.Tree || made.At(index).Object.Code is >= 0x48 and < 0x50)
            {
                made.At(index).Object = MapObject.None;
            }
        }

        if (heap is { } code)
        {
            foreach (var (x, y) in Reach().Where(node => HeapNode(node) && !made.At(node.X, node.Y).Object.Exists))
            {
                made.At(x, y).Object = new MapObject { Code = code };
                made.At(x, y).LakeMark = true;
            }
        }

        map = made;

        var game = GameFactory.Create(tables ?? WoodcuttingTests.Made(Workers()), made, everyPassTurns: everyPass);
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

        foreach (
            var (x, y, directions) in new[]
            {
                (16, 6, new[] { 0, 0 }),
                (18, 6, new[] { 5, 5, 0 }),
                (17, 3, new[] { 0, 1 }),
            }
        )
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

        Huts.PlaceAndTick(game, HutX, HutY, BuildingType.Stonecutter);

        return game;
    }

    static bool HoldsStone(Game game) => game.Flags[HutFlag].Places.Any(place => place.Good == Stone);

    [TestMethod]
    public void AStonecutterCutsAHeapOfHisReachAndLaysAStoneOnTheHutsFlag()
    {
        var game = World(0x4d, out var map);

        for (var tick = 0; tick < 6000 && !HoldsStone(game); tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(Stone, game.Flags[HutFlag].Places[0].Good, "the stone lies on the first place of the flag");
        var cut = Reach().Where(node => map.At(node.X, node.Y).Object.Code == 0x4e).ToArray();
        Assert.HasCount(1, cut, "one heap is one stage smaller");
        Assert.AreEqual(Heaps(map), Reach().Count(node => map.At(node.X, node.Y).Object.Code is 0x4d or 0x4e));
    }

    // With a turn at every pass the step back (part 2 of the cut) spans turns, and the turns after the first are on the node he
    // stepped back to, so the cut names the heap by the neighbour of that node.
    [TestMethod]
    public void EveryCutStoneEventOfACutNamesTheHeapEvenWhileTheStepBackSpansTurns()
    {
        var game = World(0x4d, out var map, everyPass: true);

        for (var tick = 0; tick < 6000 && !HoldsStone(game); tick++)
        {
            game.Tick();
        }

        var heap = Reach().Single(node => map.At(node.X, node.Y).Object.Code == 0x4e);
        var named = game
            .History.Events.All.Where(e => e.Type == EventType.CutStone)
            .Select(e => e.Other)
            .Distinct()
            .ToArray();

        Assert.HasCount(1, named, "every cut-stone event names the one heap");
        Assert.AreEqual(ThingId.OfObject("stone", heap.X, heap.Y).ToString(), named[0]);
    }

    [TestMethod]
    public void AHeapIsGoneAfterItsLastStoneAndTheNodeIsFreeAgain()
    {
        var game = World(0x4f, out var map);
        var reach = Reach().Where(node => map.At(node.X, node.Y).Object.Kind == MapObjectKind.StoneHeap).ToArray();

        for (var tick = 0; tick < 6000 && !HoldsStone(game); tick++)
        {
            game.Tick();
        }

        var gone = reach.Single(node => map.At(node.X, node.Y).Object.Kind != MapObjectKind.StoneHeap);
        Assert.AreEqual(MapObject.None, map.At(gone.X, gone.Y).Object, "no heap and nothing else stands there");
        Assert.IsFalse(map.At(gone.X, gone.Y).LakeMark, "the node is no longer marked");
        Assert.AreEqual(reach.Length - 1, Heaps(map), "no other heap was cut");
    }

    [TestMethod]
    public void WithNoHeapInReachTheWorkerWaitsInHisHutAndTheGameKeepsTicking()
    {
        var game = World(null, out _);

        for (var tick = 0; tick < 3000; tick++)
        {
            game.Tick();
        }

        var digest = game.Digest();
        var ticks = game.TickCount;
        game.Tick();

        Assert.IsInstanceOfType<LooksForStone>(
            game.Settlers[WorkerSlot].Activity,
            "he looks for a heap inside the hut"
        );
        Assert.AreEqual((HutX, HutY), (game.Settlers[WorkerSlot].X, game.Settlers[WorkerSlot].Y));
        Assert.IsTrue(game.Flags[HutFlag].Places.All(place => place.IsEmpty));
        Assert.AreEqual(ticks + 1, game.TickCount);
        Assert.AreNotEqual(digest, game.Digest(), "the game state moves on");
    }

    // The tables of ConstructionTables with a forester that needs two stones and no plank, the stone asking the class it asks there.
    static RuleTables SiteTables()
    {
        var needs = new byte[2 * RuleTables.BuildingTypeCount];
        needs[(2 * BuildingType.Forester.Code) + 1] = 2;

        return LayoutSettlersTests.MadeTables(
            buildingClass: 4,
            transportPriorities: [.. Enumerable.Range(1, RuleTables.Goods).Select(good => (byte)good)],
            buildingSizes: ConstructionTables.Sizes(),
            buildingNeeds: needs,
            goodClasses: ConstructionTables.Classes(),
            distribution: ConstructionTables.Orders(),
            plankRankBase: 0xff,
            workers: Workers()
        );
    }

    [TestMethod]
    public void AStoneGoesOverTheRoadsToTheCastleWhereNoSiteAsksForIt()
    {
        var game = World(0x4d, out _, SiteTables());
        var stones = game.CastleStock.Stones;

        for (var tick = 0; tick < 12000 && game.CastleStock.Stones == stones; tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(stones + 1, game.CastleStock.Stones, "the castle's stone count rises");
        Assert.IsFalse(HoldsStone(game), "the stone has left the flag");
    }

    [TestMethod]
    public void AStoneGoesToASiteThatAsksForStonesInsteadOfTheCastle()
    {
        var game = World(0x4d, out _, SiteTables());
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = 17,
                Y = 5,
                Building = BuildingType.Forester,
            }
        );
        game.Tick();
        var site = game.Buildings.Single(building => building.Type == BuildingType.Forester);
        var stones = game.CastleStock.Stones;

        for (var tick = 0; tick < 20000 && site.Site!.Stones < 2; tick++)
        {
            game.Tick();
        }

        Assert.AreEqual(2, site.Site!.Stones, "both stones the site asks for came from the stonecutter");
        Assert.AreEqual(stones, game.CastleStock.Stones, "none of them went to the castle");
    }
}
