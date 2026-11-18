using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Domain.Testing;
using OpenSettlers.Simulation;

namespace OpenSettlers.Adapters.Testing;

/// <summary>A game with two flags on made-up tables, for the tests of the road rules.</summary>
public static class RoadWorld
{
    /// <summary>The castle of the seed's map stands at 15,5 and its flag at 16,6; a flag goes on 18,6, two nodes east.</summary>
    public const int CastleX = 15;

    /// <summary>The row of the castle.</summary>
    public const int CastleY = 5;

    /// <summary>
    /// The map of the seed 1234,5678,9ABC with the castle placed and a flag on 18,6, on the tables of
    /// <see cref="MadeProgram.FlagClassTables"/>.
    /// </summary>
    public static Game TwoFlags()
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
        var game = GameFactory.Create(MadeProgram.FlagClassTables(), map);
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = CastleX,
                Y = CastleY,
            }
        );
        game.Tick();
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceFlag,
                X = 18,
                Y = 6,
            }
        );
        game.Tick();
        Assert.AreEqual(2, game.Flags.Count);

        return game;
    }
}
