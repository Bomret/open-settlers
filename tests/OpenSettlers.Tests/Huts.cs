using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A woodcutter's hut that stands finished, the way a layout's finished-building entry sets one up: no command gives a finished
// building (docs/rules/build-sites.md), so the tests of the rules that act on a finished hut place it through the seam the
// layout uses.
internal static class Huts
{
    /// <summary>Places a finished woodcutter's hut on the node and tells whether the placement allowed it.</summary>
    public static bool Place(Game game, int x, int y) => game.PlaceFinishedBuilding(BuildingType.Woodcutter, x, y);

    /// <summary>Places the hut as <see cref="Place"/> does, then runs the tick a command would have been applied in.</summary>
    public static bool PlaceAndTick(Game game, int x, int y)
    {
        var placed = Place(game, x, y);
        game.Tick();

        return placed;
    }
}
