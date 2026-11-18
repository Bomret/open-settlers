using OpenSettlers.Domain;
using OpenSettlers.Domain.Testing;
using OpenSettlers.Simulation;

namespace OpenSettlers.Adapters.Testing;

/// <summary>Games on the tables of <see cref="MadeProgram.HistoryTables"/>, for the tests of the game's history.</summary>
public static class HistoryWorld
{
    /// <summary>A game on the map of the seed 1234,5678,9ABC with the generated trees gone and no castle.</summary>
    public static Game NewGame()
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
        for (var index = 0; index < map.Count; index++)
        {
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        return GameFactory.Create(MadeProgram.HistoryTables(), map);
    }
}
