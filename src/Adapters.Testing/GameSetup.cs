using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Adapters.Testing;

/// <summary>
/// Setup steps of a game under test that go through the domain's public operations: they leave the game's history as it
/// was, which <see cref="Game.PlaceGood"/> does not (it records a layout event and refuses a place that holds a good).
/// </summary>
public static class GameSetup
{
    /// <summary>
    /// Puts a good on a place of a flag as the settler's put does (0x436b46): the place's destination is cleared, or set to a
    /// given one, and the place's way is as given. The flag's turn is due. It repeats the writes of
    /// <c>GoodsTransport.LayGood</c> in <c>src/Application/GoodsTransport.cs</c>; a change to one changes the other.
    /// </summary>
    /// <param name="game">The game.</param>
    /// <param name="flag">The slot of the flag in the flag table.</param>
    /// <param name="place">The place of the flag, 0 to 7.</param>
    /// <param name="good">The good.</param>
    /// <param name="destination">The place's destination, or null.</param>
    /// <param name="way">The direction the good waits to go along, or null.</param>
    public static void LayGood(this Game game, int flag, int place, Good good, int? destination = null, int? way = null)
    {
        game.Flags[flag].Places[place].Lay(good, destination, way);
        game.Flags[flag].MarkGoodsTurnDue();
    }
}
