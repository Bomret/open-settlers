using OpenSettlers.Domain;
using OpenSettlers.Domain.Testing;
using OpenSettlers.Simulation;

namespace OpenSettlers.Adapters.Testing;

/// <summary>A game with a forester's site on made-up tables, for the tests of the builder.</summary>
public static class BuilderWorld
{
    /// <summary>The column of the site.</summary>
    public const int SiteX = 17;

    /// <summary>The row of the site.</summary>
    public const int SiteY = 5;

    /// <summary>
    /// A game with a forester's site at 17,5 whose planks lie on site: the hand-out of the castle is not part of these tests. A step takes
    /// 300 clock units, more than the 128 between two turns of a settler, so a builder's turn makes one blow at most.
    /// </summary>
    public static (Game Game, Building Site) World(int planksOnSite = 2, bool everyPassTurns = false)
    {
        var game = ConstructionWorld.Create(ConstructionTables.Tables(stepTime: 300), everyPassTurns: everyPassTurns);
        ConstructionWorld.Site(game, BuildingType.Forester, SiteX, SiteY);
        var site = game.Buildings[1];
        site.FillLow = planksOnSite << 4;

        return (game, site);
    }
}
