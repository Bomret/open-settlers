using System.Linq;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

/// <summary>
/// The stonecutter's world on made-up tables (no install), in the world of <see cref="WoodcuttingWorld"/>: the castle at 15,5 with
/// its flag at 16,6, the stonecutter's hut at 16,2 with its flag at 17,3 (slot 3), and the roads of that world. The generated
/// trees and heaps are gone from the map; a heap stands where a test puts it. It stays with the tests because the hut's reach is
/// read from <c>MapGenerator.SpiralOffsets</c>, which is internal to the adapters.
/// </summary>
static class StonecuttingWorld
{
    /// <summary>The column of the stonecutter's hut.</summary>
    public const int HutX = 16;

    /// <summary>The row of the stonecutter's hut.</summary>
    public const int HutY = 2;

    /// <summary>The nodes a heap can stand on for the stonecutter: the north-west neighbour of each of the 128 nodes around the hut.</summary>
    public static (int X, int Y)[] Reach() =>
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

    /// <summary>The made-up workers: the stonecutter's building asks for the stonecutter's job with no tool.</summary>
    public static WorkerRequest?[] Workers()
    {
        var workers = new WorkerRequest?[TestCatalog.BuildingSlots];
        workers[TestCatalog.CodeOf(BuildingType.Stonecutter)] = new WorkerRequest
        {
            Job = new Stonecutter(),
            Tool = Good.None,
        };

        return workers;
    }

    /// <summary>
    /// A heap stands on the nodes of his reach whose column plus twice the row is a multiple of 7: none beside another and none on
    /// the node beside a heap, so the nodes he walks to and steps back to are free.
    /// </summary>
    public static bool HeapNode((int X, int Y) node) => (node.X + 2 * node.Y) % 7 == 0;

    /// <summary>
    /// The castle, the flags, the roads, and the stonecutter's hut; a heap of the code stands on each such free node of his reach
    /// where a code is given, and the generated trees and heaps are gone from the whole map.
    /// </summary>
    public static Game World(byte? heap, out Map map, RuleTables? tables = null, bool everyPass = false)
    {
        var made = MapGenerator.Generate(
            MadeProgram.Made(),
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

        var game = GameFactory.Create(
            tables ?? MadeProgram.WoodcuttingTables(Workers()),
            made,
            everyPassTurns: everyPass
        );
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

        Huts.PlaceAndTick(game, HutX, HutY, BuildingType.Stonecutter);

        return game;
    }
}
