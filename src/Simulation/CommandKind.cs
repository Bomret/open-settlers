namespace OpenSettlers.Simulation;

/// <summary>
/// The codes of <see cref="Command.Kind"/>. The slices that add actions add their codes here. A command of any other
/// kind is refused.
/// </summary>
public static class CommandKind
{
    /// <summary>
    /// Put the player's first castle on the node (<see cref="Command.X"/>, <see cref="Command.Y"/>). Refused where the
    /// original refuses it (<see cref="BuildSite"/>), and once the castle stands. Every other build command is refused
    /// until then.
    /// </summary>
    public const int PlaceCastle = 1;

    /// <summary>
    /// Put a flag on the node (<see cref="Command.X"/>, <see cref="Command.Y"/>) where the flag button of the original
    /// allows it (<see cref="BuildSiteResult.AllowsFlag"/>). Refused before the castle stands, outside the map, for
    /// another player, and where the node holds a flag, a building, or an object that blocks it.
    /// </summary>
    public const int PlaceFlag = 2;
}
