using System.Linq;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

/// <summary>
/// The woodcutter's world on made-up tables (no install). The map of the seed 1234,5678,9ABC has its castle at 15,5 with its
/// flag at 16,6; the hut stands north-west of the flag at 17,3 (16,2), and roads run from the castle's flag east to 18,6, from
/// there north and east to 19,4, and from 17,3 east and south-east to 19,4. It stays with the tests because the hut's reach is
/// read from <c>MapGenerator.SpiralOffsets</c>, which is internal to the adapters.
/// </summary>
static class WoodcuttingWorld
{
    /// <summary>The 128 nodes around the hut the worker draws from, as the spiral gives them.</summary>
    public static (int X, int Y)[] Reach() =>
        [
            .. Enumerable
                .Range(1, 128)
                .Select(entry => (16 + MapGenerator.SpiralOffsets[entry].Dx, 2 + MapGenerator.SpiralOffsets[entry].Dy)),
        ];

    /// <summary>
    /// The castle, the flags, the roads, and the hut; a tree stands on every node of the hut's reach where trees is true,
    /// and the generated trees are gone from the whole map.
    /// </summary>
    public static Game World(bool trees, out Map map)
    {
        map = MapGenerator.Generate(
            MadeProgram.Made(),
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

        var game = GameFactory.Create(MadeProgram.WoodcuttingTables(), map);
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
}
