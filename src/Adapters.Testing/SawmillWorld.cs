using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Application.Testing;
using OpenSettlers.Domain;
using OpenSettlers.Domain.Testing;
using OpenSettlers.Simulation;

namespace OpenSettlers.Adapters.Testing;

/// <summary>
/// A finished sawmill on made-up tables (no install), in the world of <see cref="ConstructionWorld"/>: the sawmill at 14,8 with its
/// flag at 15,9 (slot 4), which a road joins to the castle's flag at 16,6 (slot 0), and the flags 18,6 (slot 1), 19,4, and 17,3 with
/// their roads. The sawmiller is the castle's settler of the job 6.
/// </summary>
public static class SawmillWorld
{
    /// <summary>The column of the sawmill.</summary>
    public const int SawmillX = 14;

    /// <summary>The row of the sawmill.</summary>
    public const int SawmillY = 8;

    /// <summary>The slot of the castle's flag.</summary>
    public const int CastleFlag = 0;

    /// <summary>The slot of the sawmill's flag.</summary>
    public const int SawmillFlag = 4;

    /// <summary>The game with the sawmill standing and its flag joined to the castle's by a road.</summary>
    public static (Game Game, Building Sawmill) World(bool handsOutGoods = false)
    {
        var game = ConstructionWorld.Create(ConstructionTables.Tables(), handsOutGoods);
        ConstructionWorld.Do(game, CommandKind.PlaceFlag, 15, 9);
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 16, 6, [2, 2, 2, 3]);
        Assert.IsTrue(Huts.PlaceAndTick(game, SawmillX, SawmillY, BuildingType.Sawmill), "the sawmill stands");
        var sawmill = game.Buildings.Single(building => building.Type == BuildingType.Sawmill);
        Assert.AreEqual(SawmillFlag, sawmill.Flag, "the sawmill's flag is the flag at 15,9");

        return (game, sawmill);
    }

    /// <summary>Runs until the sawmiller waits inside, with the flag asking for logs.</summary>
    public static Settler SawmillerInside(Game game)
    {
        ConstructionWorld.Run(game, 1500);

        return ConstructionWorld.OfJob(game, new Sawmiller()).Single();
    }
}
