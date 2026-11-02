namespace OpenSettlers.Simulation;

/// <summary>
/// A player's action that the simulation takes at a given tick. <c>Kind</c> names the action; its codes belong to
/// the slices that add actions. <c>X</c> and <c>Y</c> are the tile the action aims at.
/// </summary>
/// <param name="Tick">The tick whose <see cref="Game.Tick"/> applies the command; counted from 0.</param>
/// <param name="Player">The player who gives the command.</param>
/// <param name="Kind">The action.</param>
/// <param name="X">The tile column.</param>
/// <param name="Y">The tile row.</param>
public readonly record struct Command(long Tick, int Player, int Kind, int X, int Y);
