namespace OpenSettlers.Simulation;

/// <summary>
/// A flag of the player's: the node it stands on. The flag's position in <see cref="Game.Flags"/> is its slot in the
/// original's flag table (<c>Table70</c>, <c>docs/rules/flags.md</c>); the same number is the link the node holds in
/// the map's <see cref="MapLayer.Resource"/> byte.
/// </summary>
/// <param name="X">The node's column.</param>
/// <param name="Y">The node's row.</param>
public readonly record struct Flag(int X, int Y);
