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
    private const int HutFlag = 3;
    private const int WorkerSlot = 12;
    private const int HutX = 16;
    private const int HutY = 2;

    private static readonly Good Stone = new(10);

    // The nodes a heap can stand on for the stonecutter: the north-west neighbour of each of the 128 nodes around the hut.
    private static (int X, int Y)[] Reach() =>
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

    private static int Heaps(Map map) =>
        Reach().Count(node => map.At(node.X, node.Y).Object.Kind == MapObjectKind.StoneHeap);

    // A heap stands on the nodes of his reach whose column plus twice the row is a multiple of 7: none beside another and none on
    // the node beside a heap, so the nodes he walks to and steps back to are free.
    private static bool HeapNode((int X, int Y) node) => (node.X + 2 * node.Y) % 7 == 0;

    // The castle, the flags, the roads, and the stonecutter's hut; a heap of the code stands on each such free node of his reach
    // where a code is given, and the generated trees and heaps are gone from the whole map.
    private static Game World(byte? heap, out Map map)
    {
        var made = MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);
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
                made.At(x, y).Object = new MapObject(code);
                made.At(x, y).LakeMark = true;
            }
        }

        map = made;

        var game = GameFactory.Create(WoodcuttingTests.Made(), made);
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

        Huts.PlaceAndTick(game, HutX, HutY, BuildingType.Stonecutter);

        return game;
    }

    private static bool HoldsStone(Game game) => game.Flags[HutFlag].Places.Any(place => place.Good == Stone);

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
}
