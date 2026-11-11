using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A finished building that stands, the way a layout's finished-building entry sets one up: no command gives a finished
// building (docs/rules/build-sites.md), so the tests of the rules that act on a finished hut place it through the seam the
// layout uses. The woodcutter's hut is the default.
internal static class Huts
{
    /// <summary>Places a finished woodcutter's hut on the node and tells whether the placement allowed it.</summary>
    public static bool Place(Game game, int x, int y) => Place(game, x, y, BuildingType.Woodcutter);

    /// <summary>Places a finished building of the type on the node and tells whether the placement allowed it.</summary>
    public static bool Place(Game game, int x, int y, BuildingType type) => game.PlaceFinishedBuilding(type, x, y);

    /// <summary>Places the hut as <see cref="Place(Game, int, int)"/> does, then runs the tick a command would have been applied in.</summary>
    public static bool PlaceAndTick(Game game, int x, int y) => PlaceAndTick(game, x, y, BuildingType.Woodcutter);

    /// <summary>Places the building as <see cref="Place(Game, int, int, BuildingType)"/> does, then runs the tick a command would have been applied in.</summary>
    public static bool PlaceAndTick(Game game, int x, int y, BuildingType type)
    {
        var placed = Place(game, x, y, type);
        game.Tick();

        return placed;
    }
}
